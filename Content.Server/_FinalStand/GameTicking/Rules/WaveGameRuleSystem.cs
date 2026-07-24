using Content.Server._FinalStand.Cleanup;
using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.FriendlyFire;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Station;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.GameTicking.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using System.Linq;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Server.Player;
using Robust.Shared.Player;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;
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
    [Dependency] private readonly MobThresholdSystem _mobThresholds = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> DoorBumpTag = "DoorBumpOpener";

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
                // If no spawners were found at prep start (e.g. map not yet loaded), keep retrying.
                if (comp.SpawnerEntities.Count == 0)
                {
                    var unlocked = new List<EntityUid>();
                    var sq2 = EntityQueryEnumerator<WaveEnemySpawnerComponent>();
                    while (sq2.MoveNext(out var spawnerUid2, out var spawner2))
                    {
                        if (comp.WaveNumber >= spawner2.FromWave)
                            unlocked.Add(spawnerUid2);
                    }
                    if (unlocked.Count > 0)
                    {
                        if (unlocked.Count > 1) RobustRandom.Shuffle(unlocked);
                        var activeCount = RobustRandom.Next(1, unlocked.Count + 1);
                        for (var i = 0; i < activeCount; i++)
                            comp.SpawnerEntities.Add(unlocked[i]);
                    }
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
                    SpawnNextBatch(uid, comp);

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

    // prep phase, phase transitons

    private void StartPrepPhase(EntityUid uid, WaveGameRuleComponent comp)
    {
        comp.Phase = WavePhase.Prep;
        comp.VoteCountdownActive = false;
        comp.VoteCountdownSoundPlayed = false;
        comp.VoteCountdownSoundTime = TimeSpan.Zero;
        comp.PhaseEndTime = Timing.CurTime + comp.PrepDuration;

        // Pre-select spawners for the upcoming wave so the CCC UI can show them during prep.
        comp.SpawnerEntities.Clear();
        var unlocked = new List<EntityUid>();
        var sq = EntityQueryEnumerator<WaveEnemySpawnerComponent>();
        while (sq.MoveNext(out var spawnerUid, out var spawner))
        {
            if (comp.WaveNumber >= spawner.FromWave)
                unlocked.Add(spawnerUid);
        }
        if (unlocked.Count == 0)
        {
            Log.Warning($"[WaveGameRule] No WaveEnemySpawner entities found! Wave {comp.WaveNumber} will be empty.");
        }
        else
        {
            if (unlocked.Count > 1)
                RobustRandom.Shuffle(unlocked);
            var activeCount = RobustRandom.Next(1, unlocked.Count + 1);
            for (var i = 0; i < activeCount; i++)
                comp.SpawnerEntities.Add(unlocked[i]);
        }

        Log.Info($"[WaveGameRule] Prep phase started. Wave {comp.WaveNumber} begins in {comp.PrepDuration.TotalSeconds}s. " +
                 $"Pre-selected {comp.SpawnerEntities.Count} spawner(s).");
        RaiseNetworkEvent(new WaveCounterUpdateEvent(comp.WavesCompleted), Filter.Broadcast());
        RaiseNetworkEvent(new FSEnemyCountEvent(0, 0), Filter.Broadcast());
        comp.NextTimerBroadcastTime = Timing.CurTime;
        RaiseNetworkEvent(new FSPrepTimerUpdateEvent((float)comp.PrepDuration.TotalSeconds, true), Filter.Broadcast());
        Log.Info($"[WaveGameRule] WaveEndSound is {(comp.WaveEndSound == null ? "NULL" : comp.WaveEndSound.ToString())}");
        if (comp.WavesCompleted > 0 && comp.WaveEndSound != null)
            _audio.PlayGlobal(comp.WaveEndSound, Filter.Broadcast(), true);
        RaiseLocalEvent(new WavePrepStartedEvent());
    }

    private void StartCombatPhase(EntityUid uid, WaveGameRuleComponent comp)
    {
        comp.Phase = WavePhase.Combat;

        // Spawners were pre-selected in StartPrepPhase; re-select only if somehow empty.
        if (comp.SpawnerEntities.Count == 0)
        {
            var unlocked = new List<EntityUid>();
            var sq = EntityQueryEnumerator<WaveEnemySpawnerComponent>();
            while (sq.MoveNext(out var spawnerUid, out var spawner))
            {
                if (comp.WaveNumber >= spawner.FromWave)
                    unlocked.Add(spawnerUid);
            }
            if (unlocked.Count > 1)
                RobustRandom.Shuffle(unlocked);
            var activeCount = RobustRandom.Next(1, unlocked.Count + 1);
            for (var i = 0; i < activeCount; i++)
                comp.SpawnerEntities.Add(unlocked[i]);
        }

        comp.CCCEntity = EntityUid.Invalid;
        var cq = EntityQueryEnumerator<FinalStandCCCComponent>();
        if (cq.MoveNext(out var cccUid, out _))
            comp.CCCEntity = cccUid;

        if (!comp.CCCEntity.IsValid())
            Log.Warning("[WaveGameRule] No FinalStandCCC entity found — enemies will not beeline to objective.");

        var playerBonus = comp.WaveNumber >= comp.PlayerBonusFromWave
            ? _playerManager.Sessions.Length * comp.PlayerEnemyBonus
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
            var giant = Spawn(bossProto, Transform(spawnerUid).Coordinates);
            EnsureComp<WaveSpawnedTagComponent>(giant);
            EnsureComp<FSEnemyDamageTrackingComponent>(giant);
            if (TryComp<HTNComponent>(giant, out var htn))
            {
                htn.Blackboard.SetValue("VisionRadius", 15f);
                htn.Blackboard.SetValue("AggroVisionRadius", 15f);
                htn.Blackboard.SetValue(NPCBlackboard.NavSmash, true);
                htn.Blackboard.SetValue(NPCBlackboard.NavPry, false);
                if (comp.CCCEntity.IsValid())
                    htn.Blackboard.SetValue(NPCBlackboard.CurrentOrderedTarget, comp.CCCEntity);
            }
            ScaleEnemyHp(giant, comp.WaveNumber);
            ScaleEnemySpeed(giant, comp.WaveNumber);
            ScaleEnemyDamage(giant, comp.WaveNumber, _playerManager.Sessions.Length);
            ScaleEnemyFireRate(giant, comp.WaveNumber);
            RaiseLocalEvent(giant, new FSEnemyHpScaledEvent()); // FINALSTAND: armor recalculates after HP scale
            comp.AliveEnemies.Add(giant);
            comp.GiantEntity = giant;
            comp.EnemyTotalThisWave++;
            Log.Info($"[WaveGameRule] Boss wave {comp.WaveNumber}: spawned {bossProto} ({giant}) at spawner {spawnerUid}.");
        }
        comp.PhaseEndTime = Timing.CurTime + comp.MaxCombatDuration;
        comp.NextSpawnTime = Timing.CurTime;

        var pool = GetDirectorPool(comp);
        Log.Info($"[WaveGameRule] Wave {comp.WaveNumber} started. Spawning {comp.EnemyTotalThisWave} enemies " +
                 $"at {comp.SpawnerEntities.Count} spawners. Director pool: {pool.Count} type(s).");

        RaiseNetworkEvent(new WaveCounterUpdateEvent(comp.WaveNumber), Filter.Broadcast());
        RaiseNetworkEvent(new FSEnemyCountEvent(0, comp.EnemyTotalThisWave), Filter.Broadcast());
        RaiseNetworkEvent(new FSPrepTimerUpdateEvent(0f, false), Filter.Broadcast());
        Log.Info($"[WaveGameRule] WaveStartSound is {(comp.WaveStartSound == null ? "NULL" : comp.WaveStartSound.ToString())}");
        if (comp.WaveStartSound != null)
            _audio.PlayGlobal(comp.WaveStartSound, Filter.Broadcast(), true);
        RaiseLocalEvent(new WaveCombatStartedEvent());
    }

    private void EndCombatPhase(EntityUid uid, WaveGameRuleComponent comp, bool isForced = false)
    {
        Log.Info($"[WaveGameRule] Wave {comp.WaveNumber} complete. Moving to prep for wave {comp.WaveNumber + 1}.");
        if (!isForced)
        {
            var ended = new WaveEndedEvent(comp.WaveNumber);
            RaiseLocalEvent(ref ended);
        }
        var waveBonus = GetCompletionBonus(comp.WaveNumber) + GetSurvivalBonus(comp.WaveNumber);
        _wallet.DistributeCredits(waveBonus);
        comp.AccumulatedSurvivalBonus += waveBonus;
        if (IsBossWave(comp.WaveNumber) && !comp.GiantApAwarded)
        {
            Log.Warning($"[WaveGameRule] Wave {comp.WaveNumber} boss fallback — Giant never died.");
        }
        comp.GiantEntity = EntityUid.Invalid;
        comp.GiantApAwarded = false;
        comp.WavesCompleted++;
        comp.WaveNumber++;
        StartPrepPhase(uid, comp);
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
        var allSpawned = comp.EnemiesSpawnedThisWave >= comp.EnemyTotalThisWave;
        var allDead = comp.AliveEnemies.Count == 0;
        Log.Debug($"[WaveGameRule] CheckWaveComplete: allSpawned={allSpawned} ({comp.EnemiesSpawnedThisWave}/{comp.EnemyTotalThisWave}), allDead={allDead} ({comp.AliveEnemies.Count} alive)");
        if (allSpawned && allDead)
            EndCombatPhase(uid, comp);
    }

    // wave spawning

    private bool IsSpawnClear(EntityCoordinates coords)
    {
        var mapCoords = _transform.ToMapCoordinates(coords);
        var ents = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(mapCoords.MapId, mapCoords.Position, 0.4f, ents, LookupFlags.Static | LookupFlags.Approximate);
        foreach (var ent in ents)
        {
            if (TryComp<PhysicsComponent>(ent, out var body) && body.Hard && body.CanCollide && body.BodyType == BodyType.Static)
                return false;
        }
        return true;
    }

    private void SpawnNextBatch(EntityUid uid, WaveGameRuleComponent comp)
    {
        var pool = GetDirectorPool(comp);
        var remaining = comp.EnemyTotalThisWave - comp.EnemiesSpawnedThisWave;
        var toSpawn = Math.Min(remaining, comp.SpawnerEntities.Count * comp.SpawnBatchSize);

        for (var i = 0; i < toSpawn; i++)
        {
            var spawnerUid = comp.SpawnerEntities[i % comp.SpawnerEntities.Count];

            var coords = Transform(spawnerUid).Coordinates;
            if (TryComp<WaveEnemySpawnerComponent>(spawnerUid, out var spawnerComp) && spawnerComp.SpawnRadius > 0f)
            {
                var found = false;
                for (var attempt = 0; attempt < 8; attempt++)
                {
                    var angle = RobustRandom.NextFloat() * MathF.Tau;
                    var radius = MathF.Sqrt(RobustRandom.NextFloat()) * spawnerComp.SpawnRadius;
                    var candidate = coords.Offset(new System.Numerics.Vector2(MathF.Cos(angle) * radius, MathF.Sin(angle) * radius));
                    if (IsSpawnClear(candidate))
                    {
                        coords = candidate;
                        found = true;
                        break;
                    }
                }
                // If all attempts hit walls, spawner centre is used as fallback.
                _ = found;
            }

            var proto = SelectEnemyProto(comp, pool);
            var enemy = Spawn(proto, coords);
            EnsureComp<WaveSpawnedTagComponent>(enemy);
            EnsureComp<FSEnemyDamageTrackingComponent>(enemy);
            if (TryComp<HTNComponent>(enemy, out var htn))
            {
                // FINALSTAND issue-2: 1000f gave zombies map-wide omniscient aggro through walls.
                // Now that NearbyHostilesQuery applies an LOS check, a larger radius is fine —
                // walls filter naturally so zombies see down open corridors but not into rooms.
                htn.Blackboard.SetValue("VisionRadius", 15f);
                htn.Blackboard.SetValue("AggroVisionRadius", 15f);
                htn.Blackboard.SetValue(NPCBlackboard.NavSmash, true);
                htn.Blackboard.SetValue(NPCBlackboard.NavPry, false);
                if (comp.CCCEntity.IsValid())
                    htn.Blackboard.SetValue(NPCBlackboard.CurrentOrderedTarget, comp.CCCEntity);

                // FINALSTAND: Bloater has higher aggro radius to prioritise player hunting over CCC beeline
                if (TryComp<FSBoomOnDeathComponent>(enemy, out var boom))
                {
                    htn.Blackboard.SetValue("AggroVisionRadius", boom.AggroVisionRadius);
                    htn.Blackboard.SetValue("VisionRadius", boom.AggroVisionRadius);
                }
            }
            ScaleEnemyHp(enemy, comp.WaveNumber);
            ScaleEnemySpeed(enemy, comp.WaveNumber);
            ScaleEnemyDamage(enemy, comp.WaveNumber, _playerManager.Sessions.Length);
            ScaleEnemyFireRate(enemy, comp.WaveNumber);
            RaiseLocalEvent(enemy, new FSEnemyHpScaledEvent()); // FINALSTAND: armor system recalculates MaxArmor after HP scale
            comp.AliveEnemies.Add(enemy);
            comp.EnemiesSpawnedThisWave++;
            Log.Info($"[WaveGameRule] Spawned {proto} ({comp.EnemiesSpawnedThisWave}/{comp.EnemyTotalThisWave}) " +
                     $"at spawner {spawnerUid}.");
        }

        var intervalSec = comp.MinSpawnInterval + RobustRandom.NextFloat() * (comp.MaxSpawnInterval - comp.MinSpawnInterval);
        comp.NextSpawnTime = Timing.CurTime + TimeSpan.FromSeconds(intervalSec);
        RaiseNetworkEvent(new FSEnemyCountEvent(comp.AliveEnemies.Count, comp.EnemyTotalThisWave), Filter.Broadcast());

        // guard for the uh the no-spawner edge case where all enemies were already counted before this batch
        CheckWaveComplete(uid, comp);
    }

    private EntProtoId SelectEnemyProto(WaveGameRuleComponent comp, List<EntProtoId> pool)
    {
        foreach (var special in comp.SpecialEnemyPool)
        {
            if (comp.WaveNumber >= special.FromWave && RobustRandom.NextFloat() < special.SpawnChance)
                return special.EnemyId;
        }
        return RobustRandom.Pick(pool);
    }

    private static List<EntProtoId> GetDirectorPool(WaveGameRuleComponent comp)
    {
        WaveEnemyConfig? match = null;
        foreach (var config in comp.EnemyConfigs)
        {
            if (config.FromWave <= comp.WaveNumber &&
                (config.ToWave == null || comp.WaveNumber <= config.ToWave))
            {
                match = config;
            }
        }
        return match?.EnemyPool ?? new List<EntProtoId> { "MobXeno" };
    }

    // event handlerss

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

        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;
            if (!comp.AliveEnemies.Remove(ent.Owner))
                continue;

            comp.TotalEnemiesKilled++;

            if (ent.Owner == comp.GiantEntity && !comp.GiantApAwarded)
            {
                comp.GiantApAwarded = true;
                Log.Info($"[WaveGameRule] Giant {ent.Owner} died.");
            }

            var baseCredits = TryComp<FSEnemyValueComponent>(ent.Owner, out var enemyVal)
                ? enemyVal.KillCredits
                : comp.KillReward;
            var killCredits = (int)(baseCredits * GetWaveKillMultiplier(comp.WaveNumber) * 0.5f);

            EntityUid? killerMind = null;
            if (args.Origin != null && _mind.TryGetMind(args.Origin.Value, out var mindId, out _))
            {
                killerMind = mindId;
                _wallet.GiveCredits(mindId, killCredits);

                // TODO(finalstand): update department ID to "TAC" once Security role is renamed
                if (IsTacRole(mindId))
                    _wallet.GiveCredits(mindId, comp.TacKillBonus);
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

            Log.Info($"[WaveGameRule] Enemy {ent.Owner} died (origin={args.Origin}). " +
                     $"{comp.AliveEnemies.Count} alive, " +
                     $"{comp.EnemyTotalThisWave - comp.EnemiesSpawnedThisWave} not yet spawned (wave {comp.WaveNumber}).");
            RaiseNetworkEvent(new FSEnemyCountEvent(comp.AliveEnemies.Count, comp.EnemyTotalThisWave), Filter.Broadcast());
            CheckWaveComplete(uid, comp);
            break;
        }
    }

    private bool IsTacRole(EntityUid mindId)
    {
        // TODO(finalstand): update department ID to "TAC" once Security role is renamed
        return _jobs.MindTryGetJob(mindId, out var job)
            && _jobs.TryGetPrimaryDepartment(job.ID, out var dept)
            && dept.ID == "Security";
    }

    private void OnWaveEnemyShutdown(EntityUid uid, WaveSpawnedTagComponent comp, ComponentShutdown args)
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var ruleUid, out var ruleComp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(ruleUid, gameRule))
                continue;
            if (!ruleComp.AliveEnemies.Remove(uid))
                continue;

            RaiseNetworkEvent(new FSEnemyCountEvent(ruleComp.AliveEnemies.Count, ruleComp.EnemyTotalThisWave), Filter.Broadcast());
            CheckWaveComplete(ruleUid, ruleComp);
            break;
        }
    }

    private void ScaleEnemyHp(EntityUid enemy, int wave)
    {
        var multiplier = GetHpMultiplier(wave);
        if (multiplier <= 1f || !TryComp<MobThresholdsComponent>(enemy, out var thresholds))
            return;

        var snapshot = new List<(FixedPoint2 damage, MobState state)>(thresholds.Thresholds.Select(kv => (kv.Key, kv.Value)));
        foreach (var (damage, state) in snapshot)
            _mobThresholds.SetMobStateThreshold(enemy, damage * multiplier, state, thresholds);
    }

    private static float GetHpMultiplier(int wave)
    {
        if (wave < 10) return 1f;
        if (wave < 20) return 2.8f;
        if (wave < 30) return 5.5f;
        return 9f;
    }

    private void ScaleEnemySpeed(EntityUid enemy, int wave)
    {
        if (wave <= 1) return;
        if (!TryComp<MovementSpeedModifierComponent>(enemy, out var move))
            return;
        const float MaxSpeedMultiplier = 2.5f;
        var multiplier = Math.Min(1f + (wave - 1) * 0.0096f, MaxSpeedMultiplier);
        _movementSpeed.ChangeBaseSpeed(enemy, move.BaseWalkSpeed * multiplier, move.BaseSprintSpeed * multiplier, move.Acceleration, move);
    }

    private void ScaleEnemyDamage(EntityUid enemy, int wave, int playerCount)
    {
        var multiplier = MathF.Min(1f + wave * (0.035f + (playerCount - 1) * 0.007f), 3.5f);
        if (multiplier <= 1f)
            return;

        if (TryComp<FSWaveDamageScaleComponent>(enemy, out var dmgScale))
            dmgScale.MeleeDamageMultiplier = multiplier;

        if (TryComp<FSFlamethrowerComponent>(enemy, out var flamer))
            flamer.ParticlesPerBurst = Math.Max(2, (int) MathF.Round(2f * multiplier));

        if (TryComp<FSTeslaZombieComponent>(enemy, out var tesla))
        {
            tesla.PrimaryDamageShock = 15f * multiplier;
            tesla.ChainDamageShock = 9f * multiplier;
        }
    }

    private void ScaleEnemyFireRate(EntityUid enemy, int wave)
    {
        var t = Math.Clamp((wave - 15f) / 5f, 0f, 1f);
        var rateMultiplier = 1f + t;
        if (rateMultiplier <= 1f)
            return;

        if (TryComp<FSFlamethrowerComponent>(enemy, out var flamer))
        {
            flamer.ParticleSpawnRate = 0.08f / rateMultiplier;
            flamer.AttackCooldown = 4f / rateMultiplier;
        }

        if (TryComp<FSTeslaZombieComponent>(enemy, out var tesla))
            tesla.AttackCooldown = 5f / rateMultiplier;
    }

    private void OnWaveStartRequest(WaveStartRequestEvent ev)
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;
            if (comp.Phase != WavePhase.Prep)
                continue;
            if (comp.VoteCountdownActive)
                return;

            comp.VoteCountdownActive = true;
            comp.VoteCountdownSoundPlayed = false;
            comp.PhaseEndTime = Timing.CurTime + TimeSpan.FromSeconds(10);
            comp.VoteCountdownSoundTime = Timing.CurTime + TimeSpan.FromSeconds(2);
            comp.NextTimerBroadcastTime = Timing.CurTime;
            return;
        }
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
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;
            var pool = GetDirectorPool(comp);
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
        return false;
    }

    public void ForceNextWave(IConsoleShell shell, int? targetWave = null)
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

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
            return;
        }

        shell.WriteError("WaveGameRule is not active.");
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
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule)) continue;

            var wavesMissed = comp.WaveNumber - 1;
            if (wavesMissed > 0)
                _wallet.GiveCredits(mindId, 1000 * wavesMissed);

            if (comp.AccumulatedSurvivalBonus > 0)
                _wallet.GiveCredits(mindId, comp.AccumulatedSurvivalBonus);

            break;
        }
    }

    // Returns the active prep-phase component so FSReadyUpSystem can read PrepDuration + TotalPlayers.
    public WaveGameRuleComponent? GetPrepComponent()
    {
        var q = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (q.MoveNext(out var uid, out var comp, out var gr))
        {
            if (GameTicker.IsGameRuleActive(uid, gr) && comp.Phase == WavePhase.Prep)
                return comp;
        }
        return null;
    }

    public void ReducePrepTimeBy(double seconds)
    {
        var q = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (q.MoveNext(out var uid, out var comp, out var gr))
        {
            if (!GameTicker.IsGameRuleActive(uid, gr)) continue;
            if (comp.Phase != WavePhase.Prep) continue;
            if (comp.VoteCountdownActive) return;
            comp.PhaseEndTime -= TimeSpan.FromSeconds(seconds);
            if (comp.PhaseEndTime < Timing.CurTime)
                comp.PhaseEndTime = Timing.CurTime;
            return;
        }
    }

    public void ToggleSpawnPause(IConsoleShell shell)
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent, GameRuleComponent>();
        while (query.MoveNext(out var uid, out var comp, out var gameRule))
        {
            if (!GameTicker.IsGameRuleActive(uid, gameRule))
                continue;

            comp.SpawnPaused = !comp.SpawnPaused;
            shell.WriteLine(comp.SpawnPaused
                ? "Wave spawning PAUSED. Existing enemies remain. Use 'pausewavespawns' again to resume."
                : "Wave spawning RESUMED.");
            return;
        }

        shell.WriteError("WaveGameRule is not active.");
    }
}
