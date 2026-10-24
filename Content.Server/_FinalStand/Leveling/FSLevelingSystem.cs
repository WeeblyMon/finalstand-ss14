using System.Linq;
using System.Text.Json;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Titles;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Leveling;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared._FinalStand.Leaderboard;
using Content.Shared._FinalStand.Onboarding;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Leveling;

public sealed partial class FSLevelingSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSTitleBuffSystem _titleBuffs = default!;
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private MedicalOps.FSMedicalStatsSystem _medicalStats = default!;
    [Dependency] private Engineering.FSEngineerStatsSystem _engineerStats = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSEnemyDamageTrackerSystem _damageTracker = default!;

    private static readonly TimeSpan LobbyRequestInterval = TimeSpan.FromSeconds(1);
    private readonly Dictionary<NetUserId, TimeSpan> _lastLobbyRequest = new();
    // Its own limit, so the panel's refresh request just before a click can't swallow the prestige.
    private readonly Dictionary<NetUserId, TimeSpan> _lastLobbyPrestige = new();

    private readonly Dictionary<EntityUid, (int Xp, int Kills, int Assists)> _roundStats = new();
    private readonly Dictionary<EntityUid, int> _roundEarned = new();

    private const int HealingScoreWeight = 10;

    private const int WaveCompletionXpPerWave = 100;
    private const int RoundEndXpPerWave = 200;
    private const int PrestigeThreshold = 50;

    // Kills and assists land hundreds of times a second in a big wave. XP is batched: the client hears about it
    // a few times a second and the database gets one transaction every few seconds, plus on detach and restart.
    private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan SaveInterval = TimeSpan.FromSeconds(5);
    private readonly List<EntityUid> _assisters = new();
    private readonly HashSet<EntityUid> _unsent = new();
    private readonly HashSet<EntityUid> _unsaved = new();
    private TimeSpan _nextSend;
    private TimeSpan _nextSave;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSEnemyDamageTrackerComponent, MobStateChangedEvent>(OnWaveEnemyDied);
        SubscribeLocalEvent<WaveSpawnedTagComponent, ComponentInit>(OnEnemyWaveTagInit);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<FSPrestigeWipedEvent>(OnPrestigeWiped);
        SubscribeNetworkEvent<FSPrestigeRequestMessage>(OnPrestigeRequest);
        SubscribeNetworkEvent<FSLevelingRequestMessage>(OnLevelingRequest);
        SubscribeLocalEvent<FSPlayerLevelComponent, FSCreditsGivenEvent>(OnCreditsGiven);

        // A server stop flushes entities without shutting systems down, so this is the last point the minds exist.
        EntityManager.BeforeEntityFlush += SaveDirty;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        EntityManager.BeforeEntityFlush -= SaveDirty;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        if (now >= _nextSend && _unsent.Count > 0)
        {
            _nextSend = now + SendInterval;
            foreach (var mindId in _unsent)
            {
                if (TryComp<FSPlayerLevelComponent>(mindId, out var lvl))
                    SendLevelingUpdate(mindId, lvl);
            }

            _unsent.Clear();
        }

        if (now >= _nextSave)
        {
            _nextSave = now + SaveInterval;
            SaveDirty();
        }
    }

    private void MarkDirty(EntityUid mindId)
    {
        _unsent.Add(mindId);
        _unsaved.Add(mindId);
    }

    private void SaveDirty()
    {
        if (_unsaved.Count == 0)
            return;

        _store.RunBatch("leveling", () =>
        {
            foreach (var mindId in _unsaved)
            {
                if (TryComp<FSPlayerLevelComponent>(mindId, out var lvl) && lvl.Loaded)
                    SaveLeveling(mindId, lvl.Level, lvl.Experience, lvl.PrestigeLevel);
            }
        });

        _unsaved.Clear();
    }

    // The leaderboard scores what was earned this round, not what's left in the wallet.
    private void OnCreditsGiven(Entity<FSPlayerLevelComponent> ent, ref FSCreditsGivenEvent args)
    {
        _roundEarned[ent.Owner] = _roundEarned.GetValueOrDefault(ent.Owner) + args.Amount;
    }

    private void OnEnemyWaveTagInit(EntityUid uid, WaveSpawnedTagComponent _, ComponentInit args)
    {
        EnsureComp<FSEnemyDamageTrackerComponent>(uid);
    }

    private void OnWaveEnemyDied(EntityUid uid, FSEnemyDamageTrackerComponent tracker, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead) return;

        _waveRule.TryGetWaveStatus(out var wave, out _);
        var baseXp = TryComp<FSEnemyValueComponent>(uid, out var val) ? val.KillCredits : 100;
        var waveMult = GetXpMultiplier(wave) * _waveRule.GetRewardMultiplier();
        var killXp = (int)(baseXp * waveMult);
        var assistXp = (int)(baseXp * 0.5f * waveMult);

        if (args.Origin.HasValue && args.Origin.Value.IsValid())
            GiveExperience(args.Origin.Value, killXp, "kill");

        _damageTracker.GetAssisters(tracker, args.Origin, _assisters);
        foreach (var assister in _assisters)
            GiveExperience(assister, assistXp, "assist");
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        var xp = (int) (WaveCompletionXpPerWave * args.WaveNumber * _waveRule.GetRewardMultiplier());

        var query = EntityQueryEnumerator<FSPlayerLevelComponent>();
        while (query.MoveNext(out var mindId, out _))
        {
            if (!TryComp<MindComponent>(mindId, out var mind)) continue;
            if (_mind.IsCharacterDeadPhysically(mind)) continue;
            GiveExperience(mindId, xp, "wave_completion", resolveBody: false);
        }
    }

    private void OnRoundEnd(RoundEndTextAppendEvent args)
    {
        if (!_waveRule.TryGetWaveStatus(out var wave, out _)) return;
        var xp = (int) (RoundEndXpPerWave * wave * _waveRule.GetRewardMultiplier());

        var query = EntityQueryEnumerator<FSPlayerLevelComponent>();
        while (query.MoveNext(out var mindId, out _))
            GiveExperience(mindId, xp, "round_end", resolveBody: false);

        var sorted = _roundStats
            .Where(kv => kv.Key.IsValid())
            .OrderByDescending(kv => kv.Value.Xp)
            .ToList();

        if (sorted.Count > 0)
        {
            args.AddLine(Loc.GetString("fs-leveling-round-xp-header"));
            foreach (var (mindId, stats) in sorted)
            {
                if (!TryComp<MindComponent>(mindId, out var mind)) continue;
                var name = mind.CharacterName ?? Loc.GetString("fs-leveling-unknown-name");
                var lvlText = TryComp<FSPlayerLevelComponent>(mindId, out var lvl)
                    ? Loc.GetString("fs-leveling-round-xp-level", ("level", lvl.Level))
                    : "";
                args.AddLine(Loc.GetString("fs-leveling-round-xp-line",
                    ("name", name.PadRight(16)),
                    ("xp", stats.Xp.ToString("N0").PadLeft(7)),
                    ("kills", stats.Kills),
                    ("assists", stats.Assists),
                    ("level", lvlText)));
            }
            args.AddLine(Loc.GetString("fs-leveling-round-xp-footer"));
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (_mind.TryGetMind(ev.Mob, out var mindId, out _))
            SetUpSpawnedMind(mindId, ev.Player);
    }

    // Spawn setup. fsdbg body calls it directly, since the real spawn event would run vanilla's handlers too.
    public void SetUpSpawnedMind(EntityUid mindId, ICommonSession player)
    {
        var lvl = EnsureComp<FSPlayerLevelComponent>(mindId);
        if (lvl.Loaded || TryLoad(mindId, lvl, player.UserId.UserId))
            SendLevelingUpdate(mindId, lvl);
    }

    // Left unloaded if the read fails: nothing is earned or saved until a later read works, which the next XP
    // payout tries again.
    private bool TryLoad(EntityUid mindId, FSPlayerLevelComponent lvl, Guid userId)
    {
        if (!_store.TryGetFullRecord(userId, out var row))
            return false;

        lvl.Level         = row.Level;
        lvl.Experience    = row.Experience;
        lvl.XpToNextLevel = XpToNextLevel(row.Level);
        lvl.PrestigeLevel = row.PrestigeLevel;
        lvl.XpMultiplier  = ComputeXpMultiplier(row.PrestigeLevel);
        lvl.Loaded        = true;

        var buffs = EnsureComp<FSPrestigeBuffsComponent>(mindId);
        if (!string.IsNullOrEmpty(row.PrestigeBuffsJson) && row.PrestigeBuffsJson != "{}")
        {
            try
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, int>>(row.PrestigeBuffsJson);
                if (dict != null)
                {
                    buffs.StoppingPower = dict.GetValueOrDefault("StoppingPower");
                    buffs.BulletStorm   = dict.GetValueOrDefault("BulletStorm");
                }
            }
            catch { }
        }

        return true;
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out _))
            return;
        if (!TryComp<FSPlayerLevelComponent>(mindId, out var lvl) || !lvl.Loaded)
            return;

        SaveLeveling(mindId, lvl.Level, lvl.Experience, lvl.PrestigeLevel);
        _unsaved.Remove(mindId);
    }

    public void SaveLeveling(EntityUid mindId, int level, int experience, int prestigeLevel)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return;

        TryComp<FSPrestigeBuffsComponent>(mindId, out var buffs);

        _store.UpsertLeveling(mind.UserId.Value.UserId, level, experience, prestigeLevel,
            SerializePrestigeBuffs(buffs));
    }

    private static string SerializePrestigeBuffs(FSPrestigeBuffsComponent? buffs)
    {
        if (buffs == null) return "{}";
        var dict = new Dictionary<string, int>();
        if (buffs.StoppingPower > 0) dict["StoppingPower"] = buffs.StoppingPower;
        if (buffs.BulletStorm   > 0) dict["BulletStorm"]   = buffs.BulletStorm;
        return JsonSerializer.Serialize(dict);
    }

    private void OnPrestigeWiped(ref FSPrestigeWipedEvent _)
    {
        var query = EntityQueryEnumerator<FSPlayerLevelComponent>();
        while (query.MoveNext(out var mindId, out var lvl))
        {
            lvl.Level         = 1;
            lvl.Experience    = 0;
            lvl.XpToNextLevel = XpToNextLevel(1);
            lvl.PrestigeLevel = 0;
            lvl.XpMultiplier  = ComputeXpMultiplier(0);
            RemComp<FSPrestigeBuffsComponent>(mindId);
            SendLevelingUpdate(mindId, lvl);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        SaveDirty();
        _unsent.Clear();
        _roundStats.Clear();
        _roundEarned.Clear();
        _lastLobbyRequest.Clear();
        _lastLobbyPrestige.Clear();

        var query = EntityQueryEnumerator<FSPlayerLevelComponent>();
        while (query.MoveNext(out _, out var lvl))
            lvl.Loaded = false;
    }

    private void OnPrestigeRequest(FSPrestigeRequestMessage _, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out MindComponent? _)
            || !TryComp<FSPlayerLevelComponent>(mindId, out var lvl))
        {
            PrestigeStored(args.SenderSession);
            return;
        }

        if (lvl.Level < PrestigeThreshold) return;

        lvl.PrestigeLevel++;
        lvl.Level = 1;
        lvl.Experience = 0;
        lvl.XpToNextLevel = XpToNextLevel(1);
        lvl.XpMultiplier = ComputeXpMultiplier(lvl.PrestigeLevel);

        SendLevelingUpdate(mindId, lvl);
        SaveLeveling(mindId, lvl.Level, lvl.Experience, lvl.PrestigeLevel);

        var charName = TryComp<MindComponent>(mindId, out var mind) && mind.CharacterName is { } characterName
            ? characterName
            : Loc.GetString("fs-leveling-unknown-name");
        AnnouncePrestige(charName, lvl.PrestigeLevel);
    }

    // The lobby panel and a perk window opened from the lobby can prestige too. There's no mind there, so the
    // stored record is reset instead, keeping its prestige buffs.
    private void PrestigeStored(ICommonSession session)
    {
        var now = _timing.CurTime;
        if (_lastLobbyPrestige.TryGetValue(session.UserId, out var last) && now - last < LobbyRequestInterval)
            return;
        _lastLobbyPrestige[session.UserId] = now;

        if (!_store.TryGetFullRecord(session.UserId.UserId, out var record) || record.Level < PrestigeThreshold)
            return;

        var prestige = record.PrestigeLevel + 1;
        _store.UpsertLeveling(session.UserId.UserId, 1, 0, prestige, record.PrestigeBuffsJson);

        RaiseNetworkEvent(new FSLevelingUpdatedEvent
        {
            Level = 1,
            Experience = 0,
            XpToNextLevel = XpToNextLevel(1),
            PrestigeLevel = prestige,
        }, Filter.SinglePlayer(session));

        AnnouncePrestige(session.Name, prestige);
    }

    private void AnnouncePrestige(string name, int prestige)
    {
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString("fs-leveling-prestiged", ("name", name), ("prestige", prestige), ("bonus", prestige * 20)),
            Color.FromHex("#FFD700"));
    }

    // Admin tooling: raw XP through the normal path (multipliers, level-up perk points).
    public void GiveRawExperience(EntityUid mindId, int amount)
        => GiveExperience(mindId, amount, "debug", resolveBody: false);

    private void GiveExperience(EntityUid playerEntity, int rawAmount, string source,
        bool resolveBody = true)
    {
        EntityUid mindId;
        if (resolveBody)
        {
            if (!_mind.TryGetMind(playerEntity, out mindId, out _))
                return;
        }
        else
        {
            mindId = playerEntity;
        }

        // An unloaded level is level 1 here, so levelling it would pay out perk points the player already had.
        if (!TryComp<FSPlayerLevelComponent>(mindId, out var lvl))
            return;

        if (!lvl.Loaded)
        {
            if (!TryComp<MindComponent>(mindId, out var mind)
                || mind.UserId is not { } user
                || !TryLoad(mindId, lvl, user.UserId))
                return;

            SendLevelingUpdate(mindId, lvl);
        }

        var amount = (int)(rawAmount * lvl.XpMultiplier * lvl.CatchUpMultiplier * _titleBuffs.ForMind(mindId).Xp);
        lvl.Experience += amount;

        _roundStats.TryGetValue(mindId, out var stats);
        _roundStats[mindId] = source switch
        {
            "kill"   => stats with { Xp = stats.Xp + amount, Kills   = stats.Kills   + 1 },
            "assist" => stats with { Xp = stats.Xp + amount, Assists = stats.Assists + 1 },
            _        => stats with { Xp = stats.Xp + amount },
        };

        var startLevel = lvl.Level;
        var totalAp = 0;

        while (lvl.Experience >= lvl.XpToNextLevel)
        {
            lvl.Experience -= lvl.XpToNextLevel;
            lvl.Level++;
            lvl.XpToNextLevel = XpToNextLevel(lvl.Level);
            var ap = FSLevelingRules.PerkPointsForReaching(lvl.Level);
            _wallet.AddPerkPoints(mindId, ap);
            totalAp += ap;
        }

        if (lvl.Level > startLevel)
        {
            AnnounceLevelUp(mindId, lvl, lvl.Level - startLevel, totalAp);

            // The perk points went to the database already; save the level with them so a crash can't pay them twice.
            if (lvl.Loaded)
                SaveLeveling(mindId, lvl.Level, lvl.Experience, lvl.PrestigeLevel);
        }

        MarkDirty(mindId);
    }

    private void AnnounceLevelUp(EntityUid mindId, FSPlayerLevelComponent lvl, int levelsGained, int totalAp)
    {
        var name = TryComp<MindComponent>(mindId, out var mind) && mind.CharacterName is { } characterName
            ? characterName
            : Loc.GetString("fs-leveling-unknown-name");
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString("fs-leveling-level-up", ("name", name), ("level", lvl.Level),
                ("prestige", lvl.PrestigeLevel), ("gained", levelsGained)),
            Color.FromHex("#FFD700"));

        if (mind?.CurrentEntity == null) return;
        if (!TryComp<ActorComponent>(mind.CurrentEntity.Value, out var actor)) return;
        RaiseNetworkEvent(new FSLevelUpNumberEvent
        {
            Target   = GetNetEntity(mind.CurrentEntity.Value),
            ApGained = totalAp,
        }, actor.PlayerSession);
    }

    private void OnLevelingRequest(FSLevelingRequestMessage msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (_mind.TryGetMind(session, out var mindId, out _)
            && TryComp<FSPlayerLevelComponent>(mindId, out var lvl))
        {
            SendLevelingUpdate(mindId, lvl);
            return;
        }

        var now = _timing.CurTime;
        if (_lastLobbyRequest.TryGetValue(session.UserId, out var last) && now - last < LobbyRequestInterval)
            return;
        _lastLobbyRequest[session.UserId] = now;

        var record = _store.GetFullRecord(session.UserId.UserId);
        RaiseNetworkEvent(new FSLevelingUpdatedEvent
        {
            Level = record.Level,
            Experience = record.Experience,
            XpToNextLevel = XpToNextLevel(record.Level),
            PrestigeLevel = record.PrestigeLevel,
        }, Filter.SinglePlayer(session));
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
            CatchUp = lvl.CatchUpMultiplier > 1f,
        }, Filter.SinglePlayer(session));
    }

    // Rookies earn XP faster until they graduate; the client shows the boost.
    public void SetCatchUpMultiplier(Entity<FSPlayerLevelComponent?> mind, float multiplier)
    {
        if (!Resolve(mind, ref mind.Comp, false))
            return;

        mind.Comp.CatchUpMultiplier = multiplier;
        SendLevelingUpdate(mind, mind.Comp);
    }

    // Admin tooling. Refuses a level that hasn't loaded: saving it would write its default prestige over the player's row.
    public bool TrySetLevel(Entity<FSPlayerLevelComponent?> mind, int level)
    {
        if (!Resolve(mind, ref mind.Comp, false) || !mind.Comp.Loaded)
            return false;

        var lvl = mind.Comp;
        lvl.Level = Math.Max(1, level);
        lvl.Experience = 0;
        lvl.XpToNextLevel = XpToNextLevel(lvl.Level);
        SaveLeveling(mind, lvl.Level, 0, lvl.PrestigeLevel);
        SendLevelingUpdate(mind, lvl);
        return true;
    }

    public (int Xp, int Kills, int Assists) GetRoundStats(EntityUid mindId)
        => _roundStats.GetValueOrDefault(mindId);

    public (int Kills, int Assists) KillsOf(EntityUid mindId)
    {
        var stats = _roundStats.GetValueOrDefault(mindId);
        return (stats.Kills, stats.Assists);
    }

    public FSLeaderboardEntry[] GetLeaderboardSnapshot()
    {
        var query = EntityQueryEnumerator<FSPlayerLevelComponent, MindComponent>();
        var entries = new List<FSLeaderboardEntry>();

        while (query.MoveNext(out var mindId, out var lvl, out var mind))
        {
            if (mind.UserId == null)
                continue;

            var stats = _roundStats.TryGetValue(mindId, out var cur)
                ? cur
                : (Xp: 0, Kills: 0, Assists: 0);
            var earned = _roundEarned.GetValueOrDefault(mindId);

            var healing = _medicalStats.GetStats(mindId).HealingPoints;
            var fortify = (int) _engineerStats.GetStats(mindId).Points;

            var score = lvl.Level * 1000 + lvl.PrestigeLevel * 2500 + stats.Xp / 5
                        + stats.Kills * 100 + stats.Assists * 40 + healing * HealingScoreWeight + fortify * HealingScoreWeight + earned;

            TryComp<FSPlayerBadgeComponent>(mind.CurrentEntity, out var badge);

            entries.Add(new FSLeaderboardEntry(
                mind.CharacterName ?? Loc.GetString("fs-leveling-unknown-name"),
                stats.Kills,
                stats.Assists,
                healing,
                stats.Xp,
                lvl.Level,
                lvl.PrestigeLevel,
                earned,
                score,
                badge?.Rookie ?? false,
                badge?.Title?.Id) { Fortify = fortify });
        }

        return FSLeaderboardEntry.Sort(entries);
    }

    public static float ComputeXpMultiplier(int prestige)
        => 1f + prestige * 0.20f;

    public static int XpToNextLevel(int level) => 1500 * level * level;

    private static float GetXpMultiplier(int wave)
    {
        if (wave < 10) return 1f;
        if (wave < 20) return 1.5f;
        if (wave < 30) return 2f;
        return 3f;
    }
}
