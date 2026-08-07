using Content.Server._FinalStand.Cleanup;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.FriendlyFire;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Station;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.GameTicking.Components;
using System.Linq;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Console;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.GameTicking.Rules;

public sealed partial class WaveGameRuleSystem : GameRuleSystem<WaveGameRuleComponent>
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly FSCorpseCleanupSystem _corpseCleaner = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly FSFriendlyFireSystem _friendlyFire = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly WaveEnemySpawningSystem _spawning = default!;

    private static readonly TimeSpan EnemyCountBroadcastInterval = TimeSpan.FromSeconds(0.25);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, MobStateChangedEvent>(OnWaveEnemyMobStateChanged);
        SubscribeLocalEvent<WaveSpawnedTagComponent, ComponentShutdown>(OnWaveEnemyShutdown);
        SubscribeLocalEvent<WaveStartRequestEvent>(OnWaveStartRequest);
        SubscribeLocalEvent<FSEnemyDamageTrackingComponent, BeforeDamageChangedEvent>(OnEnemyBeforeDamage);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    protected override void Started(EntityUid uid, WaveGameRuleComponent comp,
        GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        _friendlyFire.AssignFactionToAllPlayers();
        StartPrepPhase(uid, comp);
    }

    protected override void ActiveTick(EntityUid uid, WaveGameRuleComponent comp,
        GameRuleComponent gameRule, float frameTime)
    {
        var now = Timing.CurTime;
        switch (comp.Phase)
        {
            case WavePhase.Prep:
                // The map may not be loaded when prep starts, so retry — but at 1 Hz, not
                // every tick, or a map with no spawners scans the whole round.
                if (comp.SpawnerEntities.Count == 0 && now >= comp.NextSpawnerRetryTime)
                {
                    comp.NextSpawnerRetryTime = now + TimeSpan.FromSeconds(1);
                    _spawning.SelectSpawners(comp);
                }
                if (comp.VoteCountdownActive && !comp.VoteCountdownSoundPlayed && now >= comp.VoteCountdownSoundTime)
                {
                    comp.VoteCountdownSoundPlayed = true;
                    if (comp.WaveVoteCountdownSound != null)
                        _audio.PlayGlobal(comp.WaveVoteCountdownSound, Filter.Broadcast(), true);
                }
                if (now >= comp.PhaseEndTime)
                {
                    if (!comp.VoteCountdownActive)
                    {
                        Log.Info($"[WaveGameRule] Prep timer at zero for wave {comp.WaveNumber}, starting 10s countdown.");
                        comp.VoteCountdownActive = true;
                        comp.VoteCountdownSoundPlayed = false;
                        comp.PhaseEndTime = now + TimeSpan.FromSeconds(10);
                        comp.VoteCountdownSoundTime = now + TimeSpan.FromSeconds(2);
                        comp.NextTimerBroadcastTime = now;
                    }
                    else
                    {
                        Log.Info($"[WaveGameRule] Countdown ended for wave {comp.WaveNumber}, starting combat.");
                        StartCombatPhase(uid, comp);
                    }
                    break;
                }
                if (now >= comp.NextTimerBroadcastTime)
                {
                    var secs = Math.Max(0f, (float)(comp.PhaseEndTime - now).TotalSeconds);
                    RaiseNetworkEvent(new FSPrepTimerUpdateEvent(secs, true), Filter.Broadcast());
                    comp.NextTimerBroadcastTime = now + TimeSpan.FromSeconds(1);
                }
                break;

            case WavePhase.Combat:
                if (now >= comp.PhaseEndTime)
                {
                    Log.Warning($"[WaveGameRule] Wave {comp.WaveNumber} fallback timer expired. Forcing end.");
                    // TODO: delete or quarantine stuck enemies here.
                    EndCombatPhase(uid, comp);
                    break;
                }

                if (!comp.SpawnPaused && comp.EnemiesSpawnedThisWave < comp.EnemyTotalThisWave && now >= comp.NextSpawnTime)
                {
                    _spawning.SpawnNextBatch(uid, comp);
                    CheckWaveComplete(uid, comp);
                }

                if (now >= comp.NextHeartbeatTime)
                {
                    var timeLeft = comp.PhaseEndTime - now;
                    Log.Info($"[WaveGameRule] Wave {comp.WaveNumber} | " +
                             $"spawned {comp.EnemiesSpawnedThisWave}/{comp.EnemyTotalThisWave} | " +
                             $"alive {comp.AliveEnemies.Count} | " +
                             $"fallback in {timeLeft.TotalSeconds:F0}s");
                    comp.NextHeartbeatTime = now + TimeSpan.FromSeconds(5);
                }
                break;
        }
    }

    protected override void AppendRoundEndText(EntityUid uid, WaveGameRuleComponent comp,
        GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        base.AppendRoundEndText(uid, comp, gameRule, ref args);
        _wallet.SaveAll();
        args.AddLine(Loc.GetString("final-stand-round-end",
            ("wave", comp.WavesCompleted),
            ("killed", comp.TotalEnemiesKilled)));
    }

    // Only one WaveGameRule is ever active at a time.
    private bool TryGetActiveRule(out EntityUid uid, out WaveGameRuleComponent comp, out GameRuleComponent gameRule)
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var foundUid, out var foundComp, out var foundGameRule))
        {
            if (!GameTicker.IsGameRuleActive(foundUid, foundGameRule))
                continue;
            uid = foundUid;
            comp = foundComp;
            gameRule = foundGameRule;
            return true;
        }
        uid = EntityUid.Invalid;
        comp = default!;
        gameRule = default!;
        return false;
    }

    // prep phase, phase transitions

    private void StartPrepPhase(EntityUid uid, WaveGameRuleComponent comp)
    {
        comp.Phase = WavePhase.Prep;
        comp.VoteCountdownActive = false;
        comp.VoteCountdownSoundPlayed = false;
        comp.VoteCountdownSoundTime = TimeSpan.Zero;
        comp.PhaseEndTime = Timing.CurTime + comp.PrepDuration;

        // Pre-select spawners for the upcoming wave so the CCC UI can show them during prep.
        _spawning.SelectSpawners(comp);
        if (comp.SpawnerEntities.Count == 0)
            Log.Warning($"[WaveGameRule] No WaveEnemySpawner entities found! Wave {comp.WaveNumber} will be empty.");

        Log.Info($"[WaveGameRule] Prep phase started. Wave {comp.WaveNumber} begins in {comp.PrepDuration.TotalSeconds}s. " +
                 $"Pre-selected {comp.SpawnerEntities.Count} spawner(s).");
        RaiseNetworkEvent(new WaveCounterUpdateEvent(comp.WavesCompleted), Filter.Broadcast());
        RaiseNetworkEvent(new FSEnemyCountEvent(0, 0), Filter.Broadcast());
        comp.NextEnemyCountBroadcast = Timing.CurTime + EnemyCountBroadcastInterval;
        comp.NextTimerBroadcastTime = Timing.CurTime;
        RaiseNetworkEvent(new FSPrepTimerUpdateEvent((float)comp.PrepDuration.TotalSeconds, true), Filter.Broadcast());
        if (comp.WavesCompleted > 0 && comp.WaveEndSound != null)
            _audio.PlayGlobal(comp.WaveEndSound, Filter.Broadcast(), true);
        RaiseLocalEvent(new WavePrepStartedEvent());
    }

    private void StartCombatPhase(EntityUid uid, WaveGameRuleComponent comp)
    {
        comp.Phase = WavePhase.Combat;

        // Spawners were pre-selected in StartPrepPhase; re-select only if somehow empty.
        if (comp.SpawnerEntities.Count == 0)
            _spawning.SelectSpawners(comp);

        comp.CCCEntity = EntityUid.Invalid;
        var cq = EntityQueryEnumerator<FinalStandCCCComponent>();
        if (cq.MoveNext(out var cccUid, out _))
            comp.CCCEntity = cccUid;

        if (!comp.CCCEntity.IsValid())
            Log.Warning("[WaveGameRule] No FinalStandCCC entity found — enemies will not beeline to objective.");

        // Fixed for the wave. Sessions alone would count the lobby, ghosts and admins.
        comp.PlayersThisWave = CountActivePlayers();

        var playerBonus = comp.WaveNumber >= comp.PlayerBonusFromWave
            ? comp.PlayersThisWave * comp.PlayerEnemyBonus
            : 0;
        comp.EnemyTotalThisWave = Math.Min((int)((4 * comp.WaveNumber + 4 + playerBonus) * 1.5f), comp.MaxEnemyCap);
        // Single-corridor waves past wave 5 double the count — one lane is too easy to hold otherwise.
        if (comp.WaveNumber >= 5 && comp.SpawnerEntities.Count == 1)
            comp.EnemyTotalThisWave = Math.Min(comp.EnemyTotalThisWave * 2, comp.MaxEnemyCap);
        comp.EnemiesSpawnedThisWave = 0;
        comp.AliveEnemies.Clear();

        comp.GiantEntity = EntityUid.Invalid;
        comp.GiantApAwarded = false;
        if (IsBossWave(comp.WaveNumber) && comp.SpawnerEntities.Count > 0 && comp.BossPool.Count > 0)
        {
            var spawnerUid = comp.SpawnerEntities[0];
            var bossProto = RobustRandom.Pick(comp.BossPool);
            var giant = _spawning.SpawnWaveEnemy(bossProto, Transform(spawnerUid).Coordinates, comp);
            comp.AliveEnemies.Add(giant);
            comp.GiantEntity = giant;
            // The boss counts against the wave quota. Both counters move, or the batch loop
            // spawns a full quota of regulars on top of it.
            comp.EnemyTotalThisWave++;
            comp.EnemiesSpawnedThisWave++;
            Log.Info($"[WaveGameRule] Boss wave {comp.WaveNumber}: spawned {bossProto} ({giant}) at spawner {spawnerUid}.");
        }
        comp.PhaseEndTime = Timing.CurTime + comp.MaxCombatDuration;
        comp.NextSpawnTime = Timing.CurTime;

        var pool = WaveEnemySpawningSystem.GetDirectorPool(comp);
        Log.Info($"[WaveGameRule] Wave {comp.WaveNumber} started. Spawning {comp.EnemyTotalThisWave} enemies " +
                 $"at {comp.SpawnerEntities.Count} spawners. Director pool: {pool.Count} type(s).");

        RaiseNetworkEvent(new WaveCounterUpdateEvent(comp.WaveNumber), Filter.Broadcast());
        RaiseNetworkEvent(new FSEnemyCountEvent(comp.AliveEnemies.Count, comp.EnemyTotalThisWave), Filter.Broadcast());
        comp.NextEnemyCountBroadcast = Timing.CurTime + EnemyCountBroadcastInterval;
        RaiseNetworkEvent(new FSPrepTimerUpdateEvent(0f, false), Filter.Broadcast());
        if (comp.WaveStartSound != null)
            _audio.PlayGlobal(comp.WaveStartSound, Filter.Broadcast(), true);
        RaiseLocalEvent(new WaveCombatStartedEvent());
    }

    // isForced (forcenextwave) intentionally skips WaveEndedEvent — it's a debug escape hatch,
    // not a real wave clear, so the per-wave payouts/resets that broadcast subscribers run on a
    // normal wave end do not fire.
    private void EndCombatPhase(EntityUid uid, WaveGameRuleComponent comp, bool isForced = false)
    {
        Log.Info($"[WaveGameRule] Wave {comp.WaveNumber} complete. Moving to prep for wave {comp.WaveNumber + 1}.");
        foreach (var enemy in comp.AliveEnemies)
            QueueDel(enemy);
        comp.AliveEnemies.Clear();

        if (!isForced)
        {
            var ended = new WaveEndedEvent(comp.WaveNumber);
            RaiseLocalEvent(ref ended);
        }
        var waveBonus = GetCompletionBonus(comp.WaveNumber) + GetSurvivalBonus(comp.WaveNumber);
        _wallet.DistributeCredits(waveBonus);
        comp.AccumulatedSurvivalBonus += waveBonus;

        // The boss may have survived to the fallback timer. Pay the reward either way.
        if (IsBossWave(comp.WaveNumber) && !comp.GiantApAwarded)
        {
            Log.Warning($"[WaveGameRule] Wave {comp.WaveNumber} boss never died — paying the reward on wave end.");
            AwardBossReward(comp);
        }
        comp.GiantEntity = EntityUid.Invalid;
        comp.GiantApAwarded = false;
        comp.WavesCompleted++;
        comp.WaveNumber++;
        StartPrepPhase(uid, comp);
    }

    private void AwardBossReward(WaveGameRuleComponent comp)
    {
        comp.GiantApAwarded = true;
        if (comp.BossWavePerkReward <= 0)
            return;

        _wallet.DistributePerkPoints(comp.BossWavePerkReward);
        Log.Info($"[WaveGameRule] Boss wave {comp.WaveNumber}: awarded {comp.BossWavePerkReward} PP.");
    }

    private int CountActivePlayers()
    {
        var count = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } mob)
                continue;
            if (!HasComp<MobStateComponent>(mob) || _mobState.IsDead(mob))
                continue;
            count++;
        }
        return count;
    }

    private static bool IsBossWave(int wave) => wave % 5 == 0;

    // TODO(finalstand): tune reward scaling tiers
    private static float GetWaveKillMultiplier(int wave)
    {
        if (wave <= 5) return 1.00f;
        if (wave <= 10) return 1.50f;
        if (wave <= 15) return 2.00f;
        if (wave <= 20) return 2.75f;
        return 3.50f;
    }

    private static int GetCompletionBonus(int wave) => 100 + 100 * wave;

    private static int GetSurvivalBonus(int wave) => 450 + 50 * wave;

    private void CheckWaveComplete(EntityUid uid, WaveGameRuleComponent comp)
    {
        if (comp.EnemiesSpawnedThisWave >= comp.EnemyTotalThisWave && comp.AliveEnemies.Count == 0)
            EndCombatPhase(uid, comp);
    }

    // event handlers

    private void OnWaveEnemyMobStateChanged(Entity<WaveSpawnedTagComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        _corpseCleaner.TrackZombieDeath(ent.Owner);

        // Clear all collision so corpses don't eat bullets (MobLayer includes BulletImpassable).
        // Safe because FS zombies have MovementIgnoreGravity — floor collision is not needed.
        if (TryComp<FixturesComponent>(ent.Owner, out var fixtures))
        {
            foreach (var (key, fixture) in fixtures.Fixtures)
            {
                _physics.SetCollisionLayer(ent.Owner, key, fixture, 0, fixtures);
                _physics.SetCollisionMask(ent.Owner, key, fixture, 0, fixtures);
            }
        }

        if (!TryGetActiveRule(out var uid, out var comp, out _) || !comp.AliveEnemies.Remove(ent.Owner))
            return;

        comp.TotalEnemiesKilled++;

        if (ent.Owner == comp.GiantEntity && !comp.GiantApAwarded)
            AwardBossReward(comp);

        var baseCredits = TryComp<FSEnemyValueComponent>(ent.Owner, out var enemyVal)
            ? enemyVal.KillCredits
            : comp.KillReward;
        var killCredits = (int)(baseCredits * GetWaveKillMultiplier(comp.WaveNumber) * 0.5f);

        EntityUid? killerMind = null;
        if (args.Origin != null && _mind.TryGetMind(args.Origin.Value, out var mindId, out _))
        {
            killerMind = mindId;
            _wallet.GiveCredits(mindId, killCredits);

            if (IsSecurityRole(mindId))
                _wallet.GiveCredits(mindId, comp.SecKillBonus);
        }

        if (TryComp<FSEnemyDamageTrackingComponent>(ent.Owner, out var tracking))
        {
            var assistCredits = (int)(killCredits * 2f / 3f);
            foreach (var assistMind in tracking.AttackerMinds)
            {
                if (killerMind.HasValue && assistMind == killerMind.Value) continue;
                _wallet.GiveCredits(assistMind, assistCredits);
            }
        }

        _spawning.PushEnemyCount(comp);
        CheckWaveComplete(uid, comp);
    }

    private bool IsSecurityRole(EntityUid mindId)
    {
        return _jobs.MindTryGetJob(mindId, out var job)
            && _jobs.TryGetPrimaryDepartment(job.ID, out var dept)
            && dept.ID == "Security";
    }

    private void OnWaveEnemyShutdown(EntityUid uid, WaveSpawnedTagComponent comp, ComponentShutdown args)
    {
        if (!TryGetActiveRule(out var ruleUid, out var ruleComp, out _) || !ruleComp.AliveEnemies.Remove(uid))
            return;

        _spawning.PushEnemyCount(ruleComp);
        CheckWaveComplete(ruleUid, ruleComp);
    }

    private void OnWaveStartRequest(WaveStartRequestEvent ev)
    {
        if (!TryGetActiveRule(out _, out var comp, out _) || comp.Phase != WavePhase.Prep || comp.VoteCountdownActive)
            return;

        comp.VoteCountdownActive = true;
        comp.VoteCountdownSoundPlayed = false;
        comp.PhaseEndTime = Timing.CurTime + TimeSpan.FromSeconds(10);
        comp.VoteCountdownSoundTime = Timing.CurTime + TimeSpan.FromSeconds(2);
        comp.NextTimerBroadcastTime = Timing.CurTime;
    }

    public record struct CCCStateData(
        int WaveNumber,
        WavePhase Phase,
        float SecondsLeft,
        int AliveEnemies,
        int TotalEnemies,
        string SpawnerDirections,
        bool IsBossWave,
        string FactionDisplay,
        List<string> NextWaveEnemyTypes);

    public bool TryGetActiveState(out CCCStateData data)
    {
        data = default;
        if (!TryGetActiveRule(out _, out var comp, out _))
            return false;

        var pool = WaveEnemySpawningSystem.GetDirectorPool(comp);
        var types = pool.Select(p => p.Id).Distinct().ToList();
        if (IsBossWave(comp.WaveNumber))
            types.InsertRange(0, comp.BossPool.Select(p => p.Id).Distinct());
        foreach (var special in comp.SpecialEnemyPool)
        {
            if (comp.WaveNumber >= special.FromWave)
                types.Add(special.EnemyId.Id);
        }
        var directions = comp.SpawnerEntities.Count == 0
            ? "none"
            : string.Join("/", comp.SpawnerEntities
                .Select(s => TryComp<WaveEnemySpawnerComponent>(s, out var sc) ? sc.DirectionLabel : "?")
                .Where(d => d.Length > 0)
                .Order());
        data = new CCCStateData(
            comp.WaveNumber,
            comp.Phase,
            Math.Max(0f, (float)(comp.PhaseEndTime - Timing.CurTime).TotalSeconds),
            comp.AliveEnemies.Count,
            comp.EnemyTotalThisWave,
            directions,
            IsBossWave(comp.WaveNumber),
            comp.FactionDisplay,
            types);
        return true;
    }

    public void ForceNextWave(IConsoleShell shell, int? targetWave = null)
    {
        if (!TryGetActiveRule(out var uid, out var comp, out _))
        {
            shell.WriteError("WaveGameRule is not active.");
            return;
        }

        if (targetWave.HasValue)
        {
            comp.WaveNumber = targetWave.Value;
            comp.WavesCompleted = targetWave.Value - 1;
        }

        if (comp.Phase == WavePhase.Combat)
        {
            foreach (var enemy in comp.AliveEnemies)
                QueueDel(enemy);
            comp.AliveEnemies.Clear();
            comp.GiantApAwarded = true;
            EndCombatPhase(uid, comp, isForced: true);
        }
        else
        {
            StartCombatPhase(uid, comp);
        }

        shell.WriteLine($"Jumped to wave {comp.WaveNumber}.");
    }

    private void OnEnemyBeforeDamage(EntityUid uid, FSEnemyDamageTrackingComponent comp, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled || args.Origin == null) return;
        if (_mind.TryGetMind(args.Origin.Value, out var mindId, out _))
            comp.AttackerMinds.Add(mindId);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _)) return;
        if (!TryGetActiveRule(out _, out var comp, out _)) return;
        if (!comp.PaidCatchUpMinds.Add(mindId)) return;

        var wavesMissed = comp.WaveNumber - 1;
        if (wavesMissed > 0)
            _wallet.GiveCredits(mindId, 1000 * wavesMissed);

        if (comp.AccumulatedSurvivalBonus > 0)
            _wallet.GiveCredits(mindId, comp.AccumulatedSurvivalBonus);
    }

    // Returns the active prep-phase component so FSReadyUpSystem can read PrepDuration + TotalPlayers.
    public WaveGameRuleComponent? GetPrepComponent()
    {
        return TryGetActiveRule(out _, out var comp, out _) && comp.Phase == WavePhase.Prep ? comp : null;
    }

    public void ReducePrepTimeBy(double seconds)
    {
        if (!TryGetActiveRule(out _, out var comp, out _)) return;
        if (comp.Phase != WavePhase.Prep) return;
        if (comp.VoteCountdownActive) return;
        comp.PhaseEndTime -= TimeSpan.FromSeconds(seconds);
        if (comp.PhaseEndTime < Timing.CurTime)
            comp.PhaseEndTime = Timing.CurTime;
    }

    public void ToggleSpawnPause(IConsoleShell shell)
    {
        if (!TryGetActiveRule(out _, out var comp, out _))
        {
            shell.WriteError("WaveGameRule is not active.");
            return;
        }

        comp.SpawnPaused = !comp.SpawnPaused;
        shell.WriteLine(comp.SpawnPaused
            ? "Wave spawning PAUSED. Existing enemies remain. Use 'pausewavespawns' again to resume."
            : "Wave spawning RESUMED.");
    }
}
