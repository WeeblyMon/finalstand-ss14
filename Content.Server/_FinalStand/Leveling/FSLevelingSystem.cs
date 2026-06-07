using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.Leveling;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Leveling;

public sealed class FSLevelingSystem : EntitySystem
{
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly WaveGameRuleSystem _waveRule = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private readonly Dictionary<EntityUid, (int Xp, int Kills, int Assists)> _roundStats = new();

    private const int WaveCompletionXpPerWave = 100;
    private const int RoundEndXpPerWave = 200;
    private const int PrestigeThreshold = 50;
    private const float AssistMinContribution = 0.05f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSEnemyDamageTrackerComponent, MobStateChangedEvent>(OnWaveEnemyDied);
        SubscribeLocalEvent<WaveSpawnedTagComponent, ComponentInit>(OnEnemyWaveTagInit);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<FSPrestigeRequestMessage>(OnPrestigeRequest);
    }

    private void OnEnemyWaveTagInit(EntityUid uid, WaveSpawnedTagComponent _, ComponentInit args)
    {
        EnsureComp<FSEnemyDamageTrackerComponent>(uid);
    }

    private void OnWaveEnemyDied(EntityUid uid, FSEnemyDamageTrackerComponent tracker, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead) return;

        Log.Debug($"[FSLevel] Enemy {uid} died. Origin={args.Origin}, HasOrigin={args.Origin.HasValue}");

        // wave 0 when no active rule → 1× multiplier
        _waveRule.TryGetActiveState(out var wave);
        var baseXp = TryComp<FSEnemyValueComponent>(uid, out var val) ? val.KillCredits : 100;
        var waveMult = GetXpMultiplier(wave.WaveNumber);
        var killXp = (int)(baseXp * waveMult);
        var assistXp = (int)(baseXp * 0.5f * waveMult);

        if (args.Origin.HasValue && args.Origin.Value.IsValid())
            GiveExperience(args.Origin.Value, killXp, "kill");
        else
            Log.Debug($"[FSLevel] No valid origin — kill XP skipped for enemy {uid}");

        var totalTracked = tracker.DamageByPlayer.Values.Sum();
        if (totalTracked > 0f)
        {
            foreach (var (playerEnt, dmg) in tracker.DamageByPlayer)
            {
                if (args.Origin.HasValue && playerEnt == args.Origin.Value) continue;
                if (dmg / totalTracked < AssistMinContribution) continue;
                GiveExperience(playerEnt, assistXp, "assist");
            }
        }
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        var xp = WaveCompletionXpPerWave * args.WaveNumber;

        var query = EntityQueryEnumerator<FSPlayerLevelComponent>();
        while (query.MoveNext(out var mindId, out _))
        {
            if (!TryComp<MindComponent>(mindId, out var mind)) continue;
            // Skip players who are physically dead — wave bonus only for survivors
            if (_mind.IsCharacterDeadPhysically(mind)) continue;
            GiveExperience(mindId, xp, "wave_completion", resolveBody: false);
        }
    }

    private void OnRoundEnd(RoundEndTextAppendEvent args)
    {
        if (!_waveRule.TryGetActiveState(out var wave)) return;
        var xp = RoundEndXpPerWave * wave.WaveNumber;

        var query = EntityQueryEnumerator<FSPlayerLevelComponent>();
        while (query.MoveNext(out var mindId, out _))
            GiveExperience(mindId, xp, "round_end", resolveBody: false);

        var sorted = _roundStats
            .Where(kv => kv.Key.IsValid())
            .OrderByDescending(kv => kv.Value.Xp)
            .ToList();

        if (sorted.Count > 0)
        {
            args.AddLine("══ XP EARNED THIS ROUND ══════════════════════");
            foreach (var (mindId, stats) in sorted)
            {
                if (!TryComp<MindComponent>(mindId, out var mind)) continue;
                var name = mind.CharacterName ?? "Unknown";
                var lvlText = TryComp<FSPlayerLevelComponent>(mindId, out var lvl)
                    ? $"→ LVL {lvl.Level}"
                    : "";
                args.AddLine($"  {name,-16} {stats.Xp,7:N0} XP   {stats.Kills} kills  {stats.Assists} assists   {lvlText}");
            }
            args.AddLine("══════════════════════════════════════════════");
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _roundStats.Clear();
    }

    private void OnPrestigeRequest(FSPrestigeRequestMessage _, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out MindComponent? _)) return;
        if (!TryComp<FSPlayerLevelComponent>(mindId, out var lvl)) return;
        if (lvl.Level < PrestigeThreshold) return;

        lvl.PrestigeLevel++;
        lvl.Level = 1;
        lvl.Experience = 0;
        lvl.XpToNextLevel = XpToNextLevel(1);
        lvl.XpMultiplier = ComputeXpMultiplier(lvl.PrestigeLevel);

        SendLevelingUpdate(mindId, lvl);
        _wallet.SaveLeveling(mindId, lvl.Level, lvl.Experience, lvl.PrestigeLevel);

        var charName = TryComp<MindComponent>(mindId, out var mind)
            ? mind.CharacterName ?? "Unknown"
            : "Unknown";
        _chatManager.DispatchServerAnnouncement(
            $"{charName} has prestiged! (Prestige {lvl.PrestigeLevel}) — XP bonus now +{lvl.PrestigeLevel * 20}%.",
            Color.FromHex("#FFD700"));
    }

    private void GiveExperience(EntityUid playerEntity, int rawAmount, string source,
        bool resolveBody = true)
    {
        EntityUid mindId;
        if (resolveBody)
        {
            if (!_mind.TryGetMind(playerEntity, out mindId, out _))
            {
                Log.Debug($"[FSLevel] GiveExperience: TryGetMind failed for entity {playerEntity} (source={source})");
                return;
            }
        }
        else
        {
            mindId = playerEntity;
        }

        if (!TryComp<FSPlayerLevelComponent>(mindId, out var lvl))
        {
            Log.Debug($"[FSLevel] GiveExperience: FSPlayerLevelComponent missing on mind {mindId} (source={source})");
            return;
        }

        var amount = (int)(rawAmount * lvl.XpMultiplier);
        lvl.Experience += amount;

        _roundStats.TryGetValue(mindId, out var stats);
        _roundStats[mindId] = source switch
        {
            "kill"   => stats with { Xp = stats.Xp + amount, Kills   = stats.Kills   + 1 },
            "assist" => stats with { Xp = stats.Xp + amount, Assists = stats.Assists + 1 },
            _        => stats with { Xp = stats.Xp + amount },
        };

        var startLevel = lvl.Level;

        while (lvl.Experience >= lvl.XpToNextLevel)
        {
            lvl.Experience -= lvl.XpToNextLevel;
            lvl.Level++;
            lvl.XpToNextLevel = XpToNextLevel(lvl.Level);
            _wallet.AddAugmentPoints(mindId, lvl.Level % 5 == 0 ? 5 : 1);
            RaiseLocalEvent(mindId, new FSLevelUpEvent
            {
                MindId = mindId,
                NewLevel = lvl.Level,
                PrestigeLevel = lvl.PrestigeLevel,
            });
        }

        if (lvl.Level > startLevel)
            AnnounceLevelUp(mindId, lvl, lvl.Level - startLevel);

        SendLevelingUpdate(mindId, lvl);
        _wallet.SaveLeveling(mindId, lvl.Level, lvl.Experience, lvl.PrestigeLevel);
    }

    private void AnnounceLevelUp(EntityUid mindId, FSPlayerLevelComponent lvl, int levelsGained)
    {
        var name = TryComp<MindComponent>(mindId, out var mind)
            ? mind.CharacterName ?? "Unknown"
            : "Unknown";
        var prestigeText = lvl.PrestigeLevel > 0 ? $" (Prestige {lvl.PrestigeLevel})" : "";
        var suffix = levelsGained > 1 ? $" (+{levelsGained} levels)" : "";
        _chatManager.DispatchServerAnnouncement(
            $"{name} reached Level {lvl.Level}{prestigeText}!{suffix}",
            Color.FromHex("#FFD700"));
    }

    public void SendLevelingUpdate(EntityUid mindId, FSPlayerLevelComponent lvl)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null) return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session)) return;

        RaiseNetworkEvent(new FSLevelingUpdatedEvent
        {
            Level = lvl.Level,
            Experience = lvl.Experience,
            XpToNextLevel = lvl.XpToNextLevel,
            PrestigeLevel = lvl.PrestigeLevel,
        }, Filter.SinglePlayer(session));
    }

    public static float ComputeXpMultiplier(int prestige)
        => 1f + prestige * 0.20f;

    public static int XpToNextLevel(int level) => 1000 * level * level; // FINALSTAND: 2x vanilla — tune in playtests

    private static float GetXpMultiplier(int wave)
    {
        if (wave < 10) return 1f;
        if (wave < 20) return 1.5f;
        if (wave < 30) return 2f;
        return 3f;
    }
}
