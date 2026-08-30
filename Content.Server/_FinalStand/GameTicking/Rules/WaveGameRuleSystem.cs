using Content.Server._FinalStand.Cleanup;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.FriendlyFire;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Server._FinalStand.Mobs;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.CCC;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.Light.Components;
using Content.Shared.GameTicking.Components;
using System.Linq;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles.Jobs;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Doors.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
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
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSCorpseCleanupSystem _corpseCleaner = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private FSFriendlyFireSystem _friendlyFire = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private WaveEnemySpawningSystem _spawning = default!;
    [Dependency] private PoweredLightSystem _poweredLight = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private ApcSystem _apc = default!;

    private static readonly TimeSpan EnemyCountBroadcastInterval = TimeSpan.FromSeconds(0.25);

    private const float DefaultFlickerMin = 0.7f;
    private const float DefaultFlickerMax = 2.2f;
    private const float FlickerFraction = 0.85f;
    private const float FlickerOffDuration = 0.22f;
    private const float BlackoutEnforceInterval = 1f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, MobStateChangedEvent>(OnWaveEnemyMobStateChanged);
        SubscribeLocalEvent<WaveSpawnedTagComponent, ComponentShutdown>(OnWaveEnemyShutdown);
        SubscribeLocalEvent<WaveStartRequestEvent>(OnWaveStartRequest);
        SubscribeLocalEvent<FSEnemyDamageTrackingComponent, BeforeDamageChangedEvent>(OnEnemyBeforeDamage);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<FSRevenantExecutedEvent>(OnRevenantExecuted);
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
        TickDarkWaveOmen(comp, frameTime);
        TickBlackoutEnforcement(comp, frameTime);
        TickLightFlicker(comp, frameTime);

        switch (comp.Phase)
        {
            case WavePhase.Prep:
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

    private void StartPrepPhase(EntityUid uid, WaveGameRuleComponent comp)
    {
        comp.Phase = WavePhase.Prep;
        comp.VoteCountdownActive = false;
        comp.VoteCountdownSoundPlayed = false;
        comp.VoteCountdownSoundTime = TimeSpan.Zero;
        comp.PhaseEndTime = Timing.CurTime + comp.PrepDuration;
        comp.IsDarkWaveUpcoming = false;
        comp.LightFlickerAccum = 0f;
        comp.LightFlickerIntervalMin = DefaultFlickerMin;
        comp.LightFlickerIntervalMax = DefaultFlickerMax;

        comp.DarkWaveWarningAccum = 0f;
        comp.DarkWaveWarningFired = false;

        if (comp.ForceDarkWave)
        {
            comp.ForceDarkWave = false;
            comp.IsDarkWaveUpcoming = true;
            Log.Info($"[WaveGameRule] Dark Wave forced for wave {comp.WaveNumber}.");
        }
        else if (comp.WaveNumber == comp.GuaranteedDarkWave)
        {
            comp.IsDarkWaveUpcoming = true;
            Log.Info($"[WaveGameRule] Dark Wave guaranteed for wave {comp.WaveNumber}.");
        }
        else if (comp.WaveNumber > 5 && !IsBossWave(comp.WaveNumber))
        {
            comp.WavesSinceLastDarkWave++;
            if (comp.WavesSinceLastDarkWave >= comp.DarkWaveCooldownWaves && RobustRandom.NextFloat() < comp.DarkWaveChance)
            {
                comp.IsDarkWaveUpcoming = true;
                Log.Info($"[WaveGameRule] Dark Wave selected for wave {comp.WaveNumber}.");
            }
        }

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
            comp.EnemyTotalThisWave++;
            comp.EnemiesSpawnedThisWave++;
            Log.Info($"[WaveGameRule] Boss wave {comp.WaveNumber}: spawned {bossProto} ({giant}) at spawner {spawnerUid}.");
        }
        comp.PhaseEndTime = Timing.CurTime + comp.MaxCombatDuration;
        comp.NextSpawnTime = Timing.CurTime;

        if (comp.ForceDarkWave)
        {
            comp.ForceDarkWave = false;
            comp.IsDarkWaveUpcoming = true;
            Log.Info($"[WaveGameRule] Dark Wave forced directly into combat for wave {comp.WaveNumber}.");
        }

        if (comp.IsDarkWaveUpcoming)
        {
            comp.IsDarkWave = true;
            comp.IsDarkWaveUpcoming = false;
            comp.WavesSinceLastDarkWave = 0;
            comp.SavedMaxEnemyCap = comp.MaxEnemyCap;
            comp.MaxEnemyCap = comp.DarkWaveEnemyCap;
            comp.EnemyTotalThisWave = int.MaxValue;
            comp.NextSpawnTime = Timing.CurTime + TimeSpan.FromSeconds(5);
            comp.PhaseEndTime = Timing.CurTime + TimeSpan.FromSeconds(comp.DarkWaveDuration);
            BlackoutStation(comp);
            DepowerAllDoors(comp);
            StartDarkWaveAmbience(comp);
            RaiseNetworkEvent(new FSDarkWaveStartedEvent(comp.DarkWaveDuration), Filter.Broadcast());
            Log.Info($"[WaveGameRule] Dark Wave started for wave {comp.WaveNumber}. Cap={comp.DarkWaveEnemyCap}, duration={comp.DarkWaveDuration}s.");
        }

        var pool = WaveEnemySpawningSystem.GetDirectorPool(comp);
        Log.Info($"[WaveGameRule] Wave {comp.WaveNumber} started. Spawning {comp.EnemyTotalThisWave} enemies " +
                 $"at {comp.SpawnerEntities.Count} spawners. Director pool: {pool.Count} type(s).");

        RaiseNetworkEvent(new WaveCounterUpdateEvent(comp.WaveNumber), Filter.Broadcast());
        RaiseNetworkEvent(new FSEnemyCountEvent(comp.AliveEnemies.Count, comp.EnemyTotalThisWave), Filter.Broadcast());
        comp.NextEnemyCountBroadcast = Timing.CurTime + EnemyCountBroadcastInterval;
        RaiseNetworkEvent(new FSPrepTimerUpdateEvent(0f, false), Filter.Broadcast());
        var startSound = comp.IsDarkWave ? comp.DarkWaveStartSound : comp.WaveStartSound;
        if (startSound != null)
            _audio.PlayGlobal(startSound, Filter.Broadcast(), true);
        RaiseLocalEvent(new WaveCombatStartedEvent());
    }

    private void EndCombatPhase(EntityUid uid, WaveGameRuleComponent comp, bool isForced = false)
    {
        Log.Info($"[WaveGameRule] Wave {comp.WaveNumber} complete. Moving to prep for wave {comp.WaveNumber + 1}.");
        foreach (var enemy in comp.AliveEnemies)
            QueueDel(enemy);
        comp.AliveEnemies.Clear();

        if (comp.IsDarkWave)
        {
            comp.IsDarkWave = false;
            comp.MaxEnemyCap = comp.SavedMaxEnemyCap;
            RestoreStation(comp);
            RepowerDoors(comp);
            StopDarkWaveAmbience(comp);
            RaiseNetworkEvent(new FSDarkWaveEndedEvent(), Filter.Broadcast());
        }

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
        if (comp.IsDarkWave)
            return;
        if (comp.EnemiesSpawnedThisWave >= comp.EnemyTotalThisWave && comp.AliveEnemies.Count == 0)
            EndCombatPhase(uid, comp);
    }

    private void OnWaveEnemyMobStateChanged(Entity<WaveSpawnedTagComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        _corpseCleaner.TrackZombieDeath(ent.Owner);

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
        List<string> NextWaveEnemyTypes,
        bool IsDarkWave);

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
            types,
            comp.IsDarkWave);
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

    private void BlackoutStation(WaveGameRuleComponent comp)
    {
        comp.CutApcs.Clear();

        var query = EntityQueryEnumerator<ApcComponent, PowerNetworkBatteryComponent>();
        while (query.MoveNext(out var uid, out var apc, out var battery))
        {
            if (!apc.MainBreakerEnabled)
                continue;

            _apc.ApcToggleBreaker(uid, apc, battery);
            comp.CutApcs.Add(uid);
        }
    }

    private void RestoreStation(WaveGameRuleComponent comp)
    {
        foreach (var uid in comp.CutApcs)
        {
            if (TryComp<ApcComponent>(uid, out var apc) && !apc.MainBreakerEnabled)
                _apc.ApcToggleBreaker(uid, apc);
        }

        comp.CutApcs.Clear();
    }

    private void DepowerAllDoors(WaveGameRuleComponent comp)
    {
        comp.DepoweredDoors.Clear();

        var query = EntityQueryEnumerator<DoorComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out var uid, out _, out var receiver))
        {
            if (receiver.PowerDisabled)
                continue;

            _powerReceiver.SetPowerDisabled(uid, true, receiver);
            comp.DepoweredDoors.Add(uid);
        }
    }

    private void RepowerDoors(WaveGameRuleComponent comp)
    {
        foreach (var uid in comp.DepoweredDoors)
        {
            if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
                _powerReceiver.SetPowerDisabled(uid, false, receiver);
        }

        comp.DepoweredDoors.Clear();
    }

    private void StartDarkWaveAmbience(WaveGameRuleComponent comp)
    {
        if (comp.DarkWaveAmbienceSound == null)
            return;

        StopDarkWaveAmbience(comp);

        var stream = _audio.PlayGlobal(comp.DarkWaveAmbienceSound, Filter.Broadcast(), true,
            AudioParams.Default.WithLoop(true));

        comp.DarkWaveAmbienceStream = stream?.Entity;
    }

    private void StopDarkWaveAmbience(WaveGameRuleComponent comp)
    {
        if (comp.DarkWaveAmbienceStream is { } stream)
            _audio.Stop(stream);

        comp.DarkWaveAmbienceStream = null;
    }

    private void AnnounceDarkWave()
    {
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString("fs-dark-wave-warning"), Color.FromHex("#8800FF"));
    }

    private void TickDarkWaveOmen(WaveGameRuleComponent comp, float frameTime)
    {
        if (comp.Phase != WavePhase.Prep || !comp.IsDarkWaveUpcoming || comp.DarkWaveWarningFired)
            return;

        comp.DarkWaveWarningAccum += frameTime;
        if (comp.DarkWaveWarningAccum < comp.DarkWaveWarningDelay)
            return;

        comp.DarkWaveWarningFired = true;

        RaiseNetworkEvent(new FSDarkWaveWarningEvent(), Filter.Broadcast());
        AnnounceDarkWave();

        if (comp.DarkWaveWarningSound != null)
            _audio.PlayGlobal(comp.DarkWaveWarningSound, Filter.Broadcast(), true);
    }

    private void TickLightFlicker(WaveGameRuleComponent comp, float frameTime)
    {
        if (comp.LightsFlickeredOff)
        {
            comp.LightFlickerRestoreAccum += frameTime;
            if (comp.LightFlickerRestoreAccum >= FlickerOffDuration)
            {
                comp.LightsFlickeredOff = false;
                RestoreFlickeredLights(comp);
            }
            return;
        }

        if (comp.Phase != WavePhase.Prep || !comp.IsDarkWaveUpcoming || !comp.DarkWaveWarningFired)
            return;

        comp.LightFlickerAccum += frameTime;
        if (comp.LightFlickerAccum < comp.LightFlickerInterval)
            return;

        comp.LightFlickerAccum = 0f;
        comp.LightFlickerInterval = comp.LightFlickerIntervalMin
            + RobustRandom.NextFloat() * (comp.LightFlickerIntervalMax - comp.LightFlickerIntervalMin);
        FlickerLights(comp);
    }

    private void OnRevenantExecuted(ref FSRevenantExecutedEvent args)
    {
        FlickerAllLightsOnce();
    }

    private void TickBlackoutEnforcement(WaveGameRuleComponent comp, float frameTime)
    {
        if (!comp.IsDarkWave || comp.CutApcs.Count == 0)
            return;

        comp.BlackoutEnforceAccum += frameTime;
        if (comp.BlackoutEnforceAccum < BlackoutEnforceInterval)
            return;
        comp.BlackoutEnforceAccum = 0f;

        foreach (var uid in comp.CutApcs)
        {
            if (TryComp<ApcComponent>(uid, out var apc) && apc.MainBreakerEnabled
                && TryComp<PowerNetworkBatteryComponent>(uid, out var battery))
                _apc.ApcToggleBreaker(uid, apc, battery);
        }
    }

    public bool IsDarkWaveActive()
        => TryGetActiveRule(out _, out var comp, out _) && comp.IsDarkWave;

    public void FlickerAllLightsOnce()
    {
        if (!TryGetActiveRule(out _, out var comp, out _) || comp.LightsFlickeredOff)
            return;

        FlickerLights(comp);
    }

    private void FlickerLights(WaveGameRuleComponent comp)
    {
        comp.FlickeredLights.Clear();

        var query = EntityQueryEnumerator<PoweredLightComponent>();
        while (query.MoveNext(out var uid, out var light))
        {
            if (!light.On || RobustRandom.NextFloat() >= FlickerFraction)
                continue;

            _poweredLight.SetState(uid, false, light);
            comp.FlickeredLights.Add(uid);
        }

        comp.LightsFlickeredOff = true;
        comp.LightFlickerRestoreAccum = 0f;
    }

    private void RestoreFlickeredLights(WaveGameRuleComponent comp)
    {
        foreach (var uid in comp.FlickeredLights)
        {
            if (TryComp<PoweredLightComponent>(uid, out var light))
                _poweredLight.SetState(uid, true, light);
        }

        comp.FlickeredLights.Clear();
    }

    public void ForceDarkWave(IConsoleShell shell)
    {
        if (!TryGetActiveRule(out _, out var comp, out _))
        {
            shell.WriteError("WaveGameRule is not active.");
            return;
        }
        comp.ForceDarkWave = true;

        if (comp.Phase == WavePhase.Prep)
        {
            comp.ForceDarkWave = false;
            comp.IsDarkWaveUpcoming = true;
            comp.LightFlickerAccum = 0f;

            comp.DarkWaveWarningAccum = comp.DarkWaveWarningDelay;
            comp.DarkWaveWarningFired = false;

            shell.WriteLine("Dark Wave armed for this prep — warning and flicker start now. " +
                            "Prep runs its normal length; use forcenextwave to start it early.");
            return;
        }

        shell.WriteLine("Dark Wave forced — arms at the next prep phase.");
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
