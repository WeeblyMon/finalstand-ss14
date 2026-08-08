using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.Leveling;
using Content.Shared._FinalStand.WaveHud;
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
    [Dependency] private readonly FSPlayerDataStore _store = default!;
    [Dependency] private readonly WaveGameRuleSystem _waveRule = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private readonly Dictionary<EntityUid, (int Xp, int Kills, int Assists)> _roundStats = new();

    private long _saveTicks;
    private int _saveCount;

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
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<FSPrestigeWipedEvent>(OnPrestigeWiped);
        SubscribeNetworkEvent<FSPrestigeRequestMessage>(OnPrestigeRequest);
    }

    private void OnEnemyWaveTagInit(EntityUid uid, WaveSpawnedTagComponent _, ComponentInit args)
    {
        EnsureComp<FSEnemyDamageTrackerComponent>(uid);
    }

    private void OnWaveEnemyDied(EntityUid uid, FSEnemyDamageTrackerComponent tracker, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead) return;

        // wave 0 when no active rule → 1× multiplier
        _waveRule.TryGetActiveState(out var wave);
        var baseXp = TryComp<FSEnemyValueComponent>(uid, out var val) ? val.KillCredits : 100;
        var waveMult = GetXpMultiplier(wave.WaveNumber);
        var killXp = (int)(baseXp * waveMult);
        var assistXp = (int)(baseXp * 0.5f * waveMult);

        if (args.Origin.HasValue && args.Origin.Value.IsValid())
            GiveExperience(args.Origin.Value, killXp, "kill");

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
        if (_saveCount > 0)
        {
            var ms = _saveTicks * 1000.0 / Stopwatch.Frequency;
            Log.Info($"[FSLevel] wave {args.WaveNumber}: {_saveCount} leveling writes, {ms:F1} ms total, {ms / _saveCount:F3} ms each");
            _saveTicks = 0;
            _saveCount = 0;
        }


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

    // Leveling loads and saves its own columns of the prestige row. The wallet owns only its own.
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _))
            return;

        var lvl = EnsureComp<FSPlayerLevelComponent>(mindId);
        if (lvl.Loaded)
        {
            SendLevelingUpdate(mindId, lvl);
            return;
        }

        var row = _store.GetFullRecord(ev.Player.UserId.UserId);
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

        SendLevelingUpdate(mindId, lvl);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out _))
            return;
        if (!TryComp<FSPlayerLevelComponent>(mindId, out var lvl))
            return;

        SaveLeveling(mindId, lvl.Level, lvl.Experience, lvl.PrestigeLevel);
    }

    public void SaveLeveling(EntityUid mindId, int level, int experience, int prestigeLevel)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return;

        TryComp<FSPrestigeBuffsComponent>(mindId, out var buffs);

        // Temporary instrumentation. Every XP gain writes a row, and whether that is worth
        // batching is a question about microseconds, not a question about design taste.
        // Reported once per wave by OnWaveEnded.
        var start = Stopwatch.GetTimestamp();
        _store.UpsertLeveling(mind.UserId.Value.UserId, level, experience, prestigeLevel,
            SerializePrestigeBuffs(buffs));
        _saveTicks += Stopwatch.GetTimestamp() - start;
        _saveCount++;
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
            SendLevelingUpdate(mindId, lvl);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _roundStats.Clear();

        // Clearing Loaded makes the next spawn re-read the row, which is what carries level and
        // prestige into the new round.
        var query = EntityQueryEnumerator<FSPlayerLevelComponent>();
        while (query.MoveNext(out _, out var lvl))
            lvl.Loaded = false;
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
        SaveLeveling(mindId, lvl.Level, lvl.Experience, lvl.PrestigeLevel);

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
                return;
        }
        else
        {
            mindId = playerEntity;
        }

        if (!TryComp<FSPlayerLevelComponent>(mindId, out var lvl))
            return;

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
        var totalAp = 0;

        while (lvl.Experience >= lvl.XpToNextLevel)
        {
            lvl.Experience -= lvl.XpToNextLevel;
            lvl.Level++;
            lvl.XpToNextLevel = XpToNextLevel(lvl.Level);
            var ap = lvl.Level % 5 == 0 ? 5 : 1;
            _wallet.AddPerkPoints(mindId, ap);
            totalAp += ap;
            RaiseLocalEvent(mindId, new FSLevelUpEvent
            {
                MindId = mindId,
                NewLevel = lvl.Level,
                PrestigeLevel = lvl.PrestigeLevel,
            });
        }

        if (lvl.Level > startLevel)
            AnnounceLevelUp(mindId, lvl, lvl.Level - startLevel, totalAp);

        SendLevelingUpdate(mindId, lvl);
        SaveLeveling(mindId, lvl.Level, lvl.Experience, lvl.PrestigeLevel);
    }

    private void AnnounceLevelUp(EntityUid mindId, FSPlayerLevelComponent lvl, int levelsGained, int totalAp)
    {
        var name = TryComp<MindComponent>(mindId, out var mind)
            ? mind.CharacterName ?? "Unknown"
            : "Unknown";
        var prestigeText = lvl.PrestigeLevel > 0 ? $" (Prestige {lvl.PrestigeLevel})" : "";
        var suffix = levelsGained > 1 ? $" (+{levelsGained} levels)" : "";
        _chatManager.DispatchServerAnnouncement(
            $"{name} reached Level {lvl.Level}{prestigeText}!{suffix}",
            Color.FromHex("#FFD700"));

        if (mind?.CurrentEntity == null) return;
        if (!TryComp<ActorComponent>(mind.CurrentEntity.Value, out var actor)) return;
        RaiseNetworkEvent(new FSLevelUpNumberEvent
        {
            Target   = GetNetEntity(mind.CurrentEntity.Value),
            ApGained = totalAp,
        }, actor.PlayerSession);
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

    public static int XpToNextLevel(int level) => 3000 * level * level;

    private static float GetXpMultiplier(int wave)
    {
        if (wave < 10) return 1f;
        if (wave < 20) return 1.5f;
        if (wave < 30) return 2f;
        return 3f;
    }
}
