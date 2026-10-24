using System.Globalization;
using System.Linq;
using System.Text.Json;
using Content.Server._FinalStand.Discord;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Ladder;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Mentor;
using Content.Server._FinalStand.Objectives;
using Content.Server._FinalStand.Onboarding;
using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Titles;
using Content.Server._FinalStand.Tutorial;
using Content.Server.Administration;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Content.Shared._FinalStand.Onboarding;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Titles;
using Content.Shared._FinalStand.Tutorial;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSDebugCommand : LocalizedEntityCommands
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _protos = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PlayTimeTrackingManager _playTime = default!;
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private FSOnboardingSystem _onboarding = default!;
    [Dependency] private FSRoundPresenceSystem _presence = default!;
    [Dependency] private FSLevelingSystem _leveling = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSTitleSystem _titles = default!;
    [Dependency] private FSPerkMasterySystem _mastery = default!;
    [Dependency] private FSMentorSystem _mentor = default!;
    [Dependency] private FSNewPlayerTipSystem _tutorial = default!;
    [Dependency] private FSWaveObjectiveSystem _objectives = default!;
    [Dependency] private FSWeeklyChallengeSystem _weekly = default!;
    [Dependency] private FSLadderSystem _ladder = default!;
    [Dependency] private FSDiscordSystem _discord = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private WaveEnemySpawningSystem _spawning = default!;
    [Dependency] private SharedMindSystem _minds = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSPerkSystem _perks = default!;

    private static readonly string[] Subcommands =
    {
        "state", "hours", "fresh", "waves", "level", "xp", "title", "mastery", "mentor",
        "firstround", "tip", "tipsreset", "down", "open", "body", "starter", "objective", "weekly", "seed", "clearladder", "special", "discord",
    };

    private static readonly EntProtoId[] SpecialZombies =
    {
        "FSZombieRunner", "FSZombieArmoured", "FSZombieBloater", "FSZombieFlamethrower",
        "FSZombieTesla", "FSZombieDevastator", "FSZombieRevenant",
    };

    private static readonly EntProtoId BodyProto = "MobHuman";

    public override string Command => "fsdbg";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        var rest = args.Skip(1).ToArray();
        switch (args[0].ToLowerInvariant())
        {
            case "state": State(shell, rest); break;
            case "hours": Hours(shell, rest); break;
            case "fresh": Fresh(shell, rest); break;
            case "waves": Waves(shell, rest); break;
            case "level": Level(shell, rest); break;
            case "xp": Xp(shell, rest); break;
            case "title": Title(shell, rest); break;
            case "mastery": Mastery(shell, rest); break;
            case "mentor": Mentor(shell, rest); break;
            case "firstround": FirstRound(shell, rest); break;
            case "tip": Tip(shell, rest); break;
            case "tipsreset": TipsReset(shell, rest); break;
            case "down": Down(shell, rest); break;
            case "open": Open(shell, rest); break;
            case "body": Body(shell, rest); break;
            case "starter": Starter(shell, rest); break;
            case "objective": Objective(shell, rest); break;
            case "weekly": Weekly(shell, rest); break;
            case "seed": Seed(shell, rest); break;
            case "clearladder": ClearLadder(shell); break;
            case "special": Special(shell, rest); break;
            case "discord": Discord(shell); break;
            default: shell.WriteError($"Unknown subcommand '{args[0]}'.\n{Help}"); break;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
            return CompletionResult.FromOptions(Subcommands);

        return args[0].ToLowerInvariant() switch
        {
            "tip" when args.Length == 2 => CompletionResult.FromOptions(Enum.GetNames<FSTutorialTip>().Append("all")),
            "objective" when args.Length == 2 => CompletionResult.FromOptions(
                _protos.EnumeratePrototypes<FSWaveObjectivePrototype>().Select(p => p.ID)
                    .Concat(new[] { "random", "complete", "fail", "progress" })),
            "open" when args.Length == 2 => CompletionResult.FromOptions(new[] { "perks", "ladder" }),
            "special" when args.Length == 2 => CompletionResult.FromOptions(SpecialZombies.Select(p => p.Id).Append("all")),
            "mastery" when args.Length == 2 => CompletionResult.FromOptions(FSPerkDef.All.Keys),
            "title" when args.Length == 2 => CompletionResult.FromOptions(
                _protos.EnumeratePrototypes<FSTitlePrototype>().Select(p => p.ID)),
            "seed" when args.Length == 3 => CompletionResult.FromOptions(Enum.GetNames<FSDifficulty>()),
            _ => CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _players), "[player]"),
        };
    }

    private bool TryTarget(IConsoleShell shell, string[] args, int index, out ICommonSession session,
        out EntityUid mindId)
    {
        mindId = default;
        if (!TryTargetSession(shell, args, index, out session))
            return false;

        if (!_minds.TryGetMind(session, out mindId, out _))
        {
            shell.WriteError($"{session.Name} has no mind yet. Join the round first.");
            return false;
        }

        return true;
    }

    private bool TryTargetSession(IConsoleShell shell, string[] args, int index, out ICommonSession session)
    {
        session = default!;

        ICommonSession? found = shell.Player;
        if (args.Length > index && !_players.TryGetSessionByUsername(args[index], out found))
        {
            shell.WriteError($"No player called '{args[index]}'.");
            return false;
        }

        if (found == null)
        {
            shell.WriteError("Run this as a player or name one.");
            return false;
        }

        session = found;
        return true;
    }

    private static bool TryInt(IConsoleShell shell, string[] args, int index, out int value)
    {
        value = 0;
        if (args.Length > index && int.TryParse(args[index], out value))
            return true;

        shell.WriteError("Expected a whole number.");
        return false;
    }

    private void Refresh(ICommonSession session, EntityUid mindId)
        => _onboarding.RefreshPlayer(session, mindId, session.AttachedEntity);

    private void State(IConsoleShell shell, string[] args)
    {
        if (!TryTarget(shell, args, 0, out var session, out var mindId))
            return;

        var user = session.UserId;
        var hours = _playTime.GetOverallPlaytime(session).TotalHours;
        var onboarding = _store.GetOnboarding(user.UserId);
        var mentor = _mentor.CurrentRecord(user.UserId);
        var wallet = EntityManager.GetComponentOrNull<FSPlayerWalletComponent>(mindId);
        var lvl = EntityManager.GetComponentOrNull<FSPlayerLevelComponent>(mindId);
        var badge = session.AttachedEntity is { } body ? EntityManager.GetComponentOrNull<FSPlayerBadgeComponent>(body) : null;

        shell.WriteLine($"Player {session.Name} ({user})");
        shell.WriteLine($"  Playtime {hours:0.##} h, rookie {_onboarding.IsRookie(session)}, badge rookie {badge?.Rookie}");
        shell.WriteLine($"  Onboarding known={onboarding.Known} wasRookie={onboarding.WasRookie} starter={onboarding.StarterGranted} firstRound={onboarding.FirstRoundPaid} graduated={onboarding.Graduated}");
        shell.WriteLine($"  Level {lvl?.Level} ({lvl?.Experience}/{lvl?.XpToNextLevel} XP, catch-up x{lvl?.CatchUpMultiplier}), prestige {lvl?.PrestigeLevel}");
        shell.WriteLine($"  Perk points {wallet?.PerkPoints}, credits {wallet?.Credits}");
        shell.WriteLine($"  Waves present {_presence.GetWavesPresent(user)} (highest {_presence.GetHighestWavePresent(user)})");
        shell.WriteLine($"  Mentor eligible {_mentor.IsEligibleMentor(session)}, total {mentor.Total}, balance {mentor.Balance}, this week {mentor.WeekPoints}/{FSMentorLedger.WeekCap}");
        shell.WriteLine($"  Titles {string.Join(", ", _titles.GetOwned(user).Select(t => t.Id))}; wearing {_titles.GetEquipped(user)?.Id ?? "none"}");
        shell.WriteLine($"  Tips wanted {_tutorial.WantsTips(session)}; seen {string.Join(", ", Enum.GetValues<FSTutorialTip>().Where(t => _tutorial.HasSeen(user, t)))}");

        var mastery = _mastery.GetMastery(user).Where(kv => kv.Value > 0).Select(kv => $"{kv.Key} {kv.Value}");
        shell.WriteLine($"  Mastery {string.Join(", ", mastery)}");

        if (_waveRule.TryGetWaveStatus(out var wave, out var phase))
        {
            var modifiers = string.Join(", ", _waveRule.GetActiveModifiers().Select(m => m.ID));
            shell.WriteLine($"  Round: wave {wave} {phase}, difficulty {_waveRule.GetDifficulty()}, modifiers [{modifiers}], endless {_waveRule.IsEndless()}");
        }

        if (_objectives.Current is { } objective)
            shell.WriteLine($"  Objective {objective.Prototype.ID} {objective.Progress}/{objective.Target} {objective.Result}");

        shell.WriteLine($"  Week {FSWeeklyRules.WeekKey(DateTime.UtcNow)}: {_weekly.GetChallenge(DateTime.UtcNow)?.ID}");
    }

    private void Hours(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0 || !double.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var hours) || hours < 0)
        {
            shell.WriteError("Usage: fsdbg hours <hours> [player]");
            return;
        }

        if (!TryTarget(shell, args, 1, out var session, out var mindId))
            return;

        var delta = TimeSpan.FromHours(hours) - _playTime.GetOverallPlaytime(session);
        _playTime.AddTimeToOverallPlaytime(session, delta);
        Refresh(session, mindId);
        shell.WriteLine($"{session.Name} now has {_playTime.GetOverallPlaytime(session).TotalHours:0.##} h; rookie {_onboarding.IsRookie(session)}.");
    }

    private void Fresh(IConsoleShell shell, string[] args)
    {
        if (!TryTarget(shell, args, 0, out var session, out var mindId))
            return;

        var user = session.UserId;
        _store.DeleteProgress(user.UserId);
        _mastery.Forget(user);
        _tutorial.Forget(user);
        _playTime.AddTimeToOverallPlaytime(session, -_playTime.GetOverallPlaytime(session));

        if (!_leveling.TrySetLevel(mindId, 1))
            shell.WriteError("No loaded level data on that player, so the level was left alone.");

        if (EntityManager.TryGetComponent<FSPlayerWalletComponent>(mindId, out var wallet))
            _wallet.TryDeductPerkPoints(mindId, wallet.PerkPoints);

        Refresh(session, mindId);
        shell.WriteLine($"{session.Name} is a brand-new player again: progress wiped, 0 h, level 1. The starter grant runs on this refresh.");
    }

    private void Waves(IConsoleShell shell, string[] args)
    {
        if (!TryInt(shell, args, 0, out var waves) || !TryTarget(shell, args, 1, out var session, out _))
            return;

        _presence.SetPresence(session.UserId, Math.Max(0, waves), Math.Max(waves, _waveRule.GetWaveNumber()));
        shell.WriteLine($"{session.Name} now counts as present for {waves} wave(s) this round.");
    }

    private void Level(IConsoleShell shell, string[] args)
    {
        if (!TryInt(shell, args, 0, out var level) || !TryTarget(shell, args, 1, out var session, out var mindId))
            return;

        if (!_leveling.TrySetLevel(mindId, level))
        {
            shell.WriteError("No loaded level data on that player.");
            return;
        }

        shell.WriteLine($"{session.Name} is now level {Math.Max(1, level)}.");
    }

    private void Xp(IConsoleShell shell, string[] args)
    {
        if (!TryInt(shell, args, 0, out var amount) || !TryTarget(shell, args, 1, out var session, out var mindId))
            return;

        _leveling.GiveRawExperience(mindId, amount);
        shell.WriteLine($"Gave {session.Name} {amount} raw XP (multipliers apply, level-ups pay as normal).");
    }

    private void Title(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError("Usage: fsdbg title <title id> [player]");
            return;
        }

        if (!_protos.HasIndex<FSTitlePrototype>(args[0]))
        {
            shell.WriteError($"No title '{args[0]}'.");
            return;
        }

        if (!TryTargetSession(shell, args, 1, out var session))
            return;

        shell.WriteLine(_titles.Grant(session.UserId, args[0])
            ? $"Granted {args[0]} to {session.Name}."
            : $"{session.Name} already owns {args[0]}.");
    }

    private void Mastery(IConsoleShell shell, string[] args)
    {
        if (args.Length < 2 || !FSPerkDef.All.ContainsKey(args[0]) || !int.TryParse(args[1], out var kills))
        {
            shell.WriteError("Usage: fsdbg mastery <perk id> <kills to add> [player]");
            return;
        }

        if (!TryTarget(shell, args, 2, out var session, out _))
            return;

        _mastery.AddKills(session.UserId, args[0], kills);
        _mastery.Flush();
        var total = _mastery.GetMastery(session.UserId).GetValueOrDefault(args[0]);
        shell.WriteLine($"{session.Name}: {args[0]} mastery {total} kills ({FSPerkMastery.Tier(total)}). Reopen the perk shop to see it.");
    }

    private void Mentor(IConsoleShell shell, string[] args)
    {
        if (!TryInt(shell, args, 0, out var points) || !TryTargetSession(shell, args, 1, out var session))
            return;

        var record = _mentor.CurrentRecord(session.UserId.UserId);
        _store.SaveMentor(session.UserId.UserId, record with
        {
            Total = Math.Max(0, record.Total + points),
            Balance = Math.Max(0, record.Balance + points),
        });
        var after = _mentor.CurrentRecord(session.UserId.UserId);
        shell.WriteLine($"{session.Name}: mentor total {after.Total}, balance {after.Balance}.");
    }

    private void FirstRound(IConsoleShell shell, string[] args)
    {
        if (!TryTarget(shell, args, 0, out var session, out var mindId))
            return;

        shell.WriteLine(_onboarding.TryPayFirstRound(session, mindId)
            ? $"Paid {session.Name}'s first-round reward."
            : $"Not paid: needs a rookie record, {FSRookieRules.FirstRoundMinWaves}+ waves present (now {_presence.GetWavesPresent(session.UserId)}), and not already paid.");
    }

    private void Tip(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError("Usage: fsdbg tip <tip|all> [player]");
            return;
        }

        if (!TryTarget(shell, args, 1, out var session, out _))
            return;

        var tips = args[0].Equals("all", StringComparison.OrdinalIgnoreCase)
            ? Enum.GetValues<FSTutorialTip>()
            : Enum.TryParse<FSTutorialTip>(args[0], true, out var one) ? new[] { one } : Array.Empty<FSTutorialTip>();

        if (tips.Length == 0)
        {
            shell.WriteError($"No tip '{args[0]}'. Options: {string.Join(", ", Enum.GetNames<FSTutorialTip>())}.");
            return;
        }

        foreach (var tip in tips)
            EntityManager.EntityNetManager.SendSystemNetworkMessage(new FSTutorialTipEvent { Tip = tip }, session.Channel);
        shell.WriteLine($"Sent {tips.Length} tip card(s) to {session.Name}. These don't count as seen.");
    }

    private void TipsReset(IConsoleShell shell, string[] args)
    {
        if (!TryTarget(shell, args, 0, out var session, out _))
            return;

        _tutorial.ResetSeen(session.UserId);
        shell.WriteLine($"{session.Name} has seen no tips now. Rookies get them again; veterans need 'tutorial on'.");
    }

    private void Down(IConsoleShell shell, string[] args)
    {
        if (!TryTarget(shell, args, 0, out var session, out _))
            return;

        if (session.AttachedEntity is not { } body)
        {
            shell.WriteError($"{session.Name} has no body.");
            return;
        }

        _mobState.ChangeMobState(body, MobState.Critical);
        shell.WriteLine($"{session.Name} is down. Use 'rejuvenate' to get back up.");
    }

    private void Open(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0 || !TryTarget(shell, args, 1, out var session, out _))
        {
            if (args.Length == 0)
                shell.WriteError("Usage: fsdbg open <perks|ladder> [player]");
            return;
        }

        EntityEventArgs? message = args[0].ToLowerInvariant() switch
        {
            "perks" => new FSOpenPerkShopEvent(),
            "ladder" => new FSOpenLadderEvent(),
            _ => null,
        };

        if (message == null)
        {
            shell.WriteError("Pick perks or ladder.");
            return;
        }

        if (message is FSOpenPerkShopEvent && session.AttachedEntity is { } body)
        {
            var shops = EntityManager.EntityQueryEnumerator<FSPerkShopComponent, TransformComponent>();
            if (shops.MoveNext(out _, out _, out var shopXform))
                _transform.SetCoordinates(body, shopXform.Coordinates.Offset(new System.Numerics.Vector2(0f, -1f)));
        }

        EntityManager.EntityNetManager.SendSystemNetworkMessage(message, session.Channel);
        shell.WriteLine($"Opened the {args[0]} window for {session.Name}.");
    }

    private void Body(IConsoleShell shell, string[] args)
    {
        if (!TryTarget(shell, args, 0, out var session, out var mindId))
            return;

        if (session.AttachedEntity is not { } current)
        {
            shell.WriteError($"{session.Name} isn't attached to anything.");
            return;
        }

        var body = EntityManager.SpawnEntity(BodyProto, EntityManager.GetComponent<TransformComponent>(current).Coordinates);
        _minds.TransferTo(mindId, body, ghostCheckOverride: true);

        // Taking the body already loaded the perks; the rest is what a spawn sets up.
        _wallet.SetUpSpawnedMind(mindId, session);
        _leveling.SetUpSpawnedMind(mindId, session);
        Refresh(session, mindId);

        shell.WriteLine($"{session.Name} now has a body, set up as if they had just spawned.");
    }

    private void Starter(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0 || !TryTarget(shell, args, 1, out var session, out _))
        {
            if (args.Length == 0)
                shell.WriteError("Usage: fsdbg starter <build id> [player]");
            return;
        }

        _perks.ApplyStarterBuild(session, args[0]);
        shell.WriteLine($"Applied {args[0]} for {session.Name} with the PP they have.");
    }

    private void Objective(IConsoleShell shell, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteError("Usage: fsdbg objective <id|random|complete|fail|progress <n>>");
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "random":
                shell.WriteLine(_objectives.TryStartRandom() ? "Started a random bonus objective." : "Couldn't start one (needs a wave in progress and none active).");
                return;
            case "complete":
            case "fail":
                _objectives.ForceResult(args[0].Equals("complete", StringComparison.OrdinalIgnoreCase));
                shell.WriteLine(_objectives.Current is { } done ? $"Objective is now {done.Result}." : "No objective running.");
                return;
            case "progress":
                if (_objectives.Current is not { } current || !TryInt(shell, args, 1, out var progress))
                {
                    shell.WriteError("Needs a running objective and a number.");
                    return;
                }

                if (!_objectives.SetProgress(progress))
                {
                    shell.WriteError($"The objective is already {current.Result}.");
                    return;
                }

                shell.WriteLine($"Progress set to {current.Progress}/{current.Target}. The next kill or check settles it.");
                return;
        }

        if (!_protos.TryIndex<FSWaveObjectivePrototype>(args[0], out var proto))
        {
            shell.WriteError($"No objective '{args[0]}'.");
            return;
        }

        shell.WriteLine(_objectives.TryStart(proto) ? $"Started {proto.ID}." : "Couldn't start it (needs a wave in progress and none active).");
    }

    private void Weekly(IConsoleShell shell, string[] args)
    {
        var at = DateTime.UtcNow;
        if (args.Length > 0 && !DateTime.TryParse(string.Join(' ', args), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out at))
        {
            shell.WriteError("Usage: fsdbg weekly [yyyy-MM-dd [HH:mm]]  (UTC)");
            return;
        }

        var challenge = _weekly.GetChallenge(at);
        shell.WriteLine($"{at:yyyy-MM-dd HH:mm} UTC is week {FSWeeklyRules.WeekKey(at)} (starts {FSWeeklyRules.WeekStart(at):yyyy-MM-dd}).");
        shell.WriteLine($"Challenge: {challenge?.ID ?? "none"}; last call window: {FSWeeklyRules.IsLastCall(at)}.");
    }

    private void Seed(IConsoleShell shell, string[] args)
    {
        if (!TryInt(shell, args, 0, out var count))
        {
            shell.WriteError("Usage: fsdbg seed <count> [difficulty] [players] [weekly]");
            return;
        }

        var difficulty = args.Length > 1 && Enum.TryParse<FSDifficulty>(args[1], true, out var d) ? d : FSDifficulty.Normal;
        var players = args.Length > 2 && int.TryParse(args[2], out var p) ? Math.Max(1, p) : 3;
        var weekly = args.Length > 3 && args[3].Equals("weekly", StringComparison.OrdinalIgnoreCase)
            ? FSWeeklyRules.WeekKey(DateTime.UtcNow)
            : null;

        string[] names = { "Ada", "Bo", "Cy", "Di", "Eve", "Fox", "Gus", "Hal", "Ivy", "Jo", "Kai", "Lu" };
        for (var i = 0; i < Math.Clamp(count, 1, 200); i++)
        {
            var crew = string.Join(", ", Enumerable.Range(0, players).Select(_ => _random.Pick(names)).Distinct());
            var waves = _random.Next(3, 36);
            var run = new FSRunRecord(crew, difficulty, FSLadder.BracketFor(players), waves, waves >= 30,
                _random.Next(600, 7200), _random.Next(100, 6000), DateTime.UtcNow.AddMinutes(-_random.Next(1, 20000)), weekly);
            _ladder.RecordRun(run);
        }

        shell.WriteLine($"Seeded {count} {difficulty} runs for {players} player(s){(weekly != null ? $" on weekly {weekly}" : "")}.");
    }

    private void Special(IConsoleShell shell, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } body)
        {
            shell.WriteError("Run this as a player with a body.");
            return;
        }

        var query = EntityManager.EntityQueryEnumerator<WaveGameRuleComponent>();
        if (!query.MoveNext(out var rule))
        {
            shell.WriteError("Start the wave rule first.");
            return;
        }

        var which = args.Length == 0 || args[0].Equals("all", StringComparison.OrdinalIgnoreCase)
            ? SpecialZombies
            : new EntProtoId[] { args[0] };

        var coords = EntityManager.GetComponent<TransformComponent>(body).Coordinates;
        foreach (var proto in which)
        {
            if (!_protos.HasIndex(proto))
            {
                shell.WriteError($"No entity '{proto}'.");
                continue;
            }

            var offset = new System.Numerics.Vector2(_random.NextFloat(-4f, 4f), _random.NextFloat(3f, 6f));
            _spawning.SpawnWaveEnemy(proto, coords.Offset(offset), rule);
        }

        shell.WriteLine($"Spawned {which.Length} wave enemy(s) near you. Each special type shows its tip the first time per round.");
    }

    private void ClearLadder(IConsoleShell shell)
    {
        _store.DeleteAllRuns();
        _ladder.RefreshBoards();
        shell.WriteLine("Ladder cleared.");
    }

    private void Discord(IConsoleShell shell)
    {
        if (_discord.LastPayload is not { } payload)
        {
            shell.WriteLine("No Discord payload built yet. Finish a round first.");
            return;
        }

        shell.WriteLine(JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }));
    }
}
