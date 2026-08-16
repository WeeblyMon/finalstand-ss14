using System.Numerics;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Components;
using Content.Shared.Ghost.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

// Orchestrates when a wave zombie needs a new breach target; delegates scoring to FSBreachEvaluator.
public sealed partial class FSBreachTargetSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSBreachEvaluator _evaluator = default!;

    private const float TickInterval = 0.1f;
    private float _accumulator;
    private float _baseZombieDamage = 10f;

    // Reused across ticks — these queries run at 10 Hz per zombie and must not allocate.
    private readonly HashSet<Entity<MobStateComponent>> _mobBuffer = new();
    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _doorBuffer = new();

    private static readonly EntProtoId ZombieNormalProto = "FSZombieNormal";

    private const float DoorOpenReplanRadius = 6f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, ComponentStartup>(OnWaveEnemyStartup);
        SubscribeLocalEvent<GameRuleEndedEvent>(OnWaveRuleEnded);
        SubscribeLocalEvent<DoorComponent, DoorStateChangedEvent>(OnDoorStateChanged);
        SubscribeLocalEvent<DoorComponent, ComponentShutdown>(OnDoorShutdown);
        if (_prototype.TryIndex<EntityPrototype>(ZombieNormalProto, out var proto)
            && proto.Components.TryGetValue(Factory.GetComponentName<MeleeWeaponComponent>(), out var entry)
            && entry.Component is MeleeWeaponComponent melee)
        {
            _baseZombieDamage = melee.Damage.GetTotal().Float();
        }
        else
        {
            Logger.Warning("[FSBreachTargetSystem] Could not read BaseZombieMeleeDamage from FSZombieNormal prototype — using default 10f");
        }
    }

    private void OnWaveEnemyStartup(EntityUid uid, WaveSpawnedTagComponent _, ComponentStartup args)
    {
        EnsureComp<FSBreachStateComponent>(uid);
    }

    private void OnWaveRuleEnded(ref GameRuleEndedEvent args)
    {
        if (!HasComp<WaveGameRuleComponent>(args.RuleEntity)) return;
        var query = EntityQueryEnumerator<FSBreachStateComponent>();
        while (query.MoveNext(out _, out var state))
        {
            state.SelectionHistory.Clear();
            state.Blacklist.Clear();
        }
    }

    private void OnDoorStateChanged(EntityUid doorUid, DoorComponent door, ref DoorStateChangedEvent args)
    {
        if (args.State != DoorState.Open)
            return;
        ForceNearbyReplan(doorUid);
    }

    // Covers the smash case: a destroyed door deletes the entity with no Open transition.
    private void OnDoorShutdown(EntityUid doorUid, DoorComponent door, ComponentShutdown args)
    {
        ForceNearbyReplan(doorUid);
    }

    private void ForceNearbyReplan(EntityUid doorUid)
    {
        var xform = Transform(doorUid);
        if (xform.MapID == MapId.Nullspace)
            return;

        var epicenter = new MapCoordinates(_transform.GetWorldPosition(xform), xform.MapID);
        var nearby = new HashSet<Entity<WaveSpawnedTagComponent>>();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(epicenter, DoorOpenReplanRadius, nearby);

        foreach (var (npcUid, _) in nearby)
        {
            if (TryComp<HTNComponent>(npcUid, out var htn))
            {
                if (htn.Blackboard.ContainsKey(FSAIBlackboardKeys.BreachTarget))
                    ClearBreachTarget(htn.Blackboard);
                _htn.Replan(htn);
            }
            if (TryComp<NPCSteeringComponent>(npcUid, out var steering))
                steering.CurrentPath.Clear();
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < TickInterval) return;
        _accumulator -= TickInterval;
        var query = EntityQueryEnumerator<ActiveNPCComponent, WaveSpawnedTagComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var htn))
            Tick(uid, htn, TickInterval);
    }

    private void Tick(EntityUid uid, HTNComponent htn, float dt)
    {
        var bb = htn.Blackboard;
        TickAttackLock(bb, dt);
        TickBreachCooldown(bb, dt);
        CheckBreachInterrupts(uid, htn, bb);
        CheckPathProgress(uid, htn, bb, dt);
    }

    private void TickBreachCooldown(NPCBlackboard bb, float dt)
    {
        if (!bb.TryGetValue<float>(FSAIBlackboardKeys.BreachCooldown, out var cd, EntityManager)) return;
        cd -= dt;
        if (cd <= 0f) bb.Remove<float>(FSAIBlackboardKeys.BreachCooldown);
        else bb.SetValue(FSAIBlackboardKeys.BreachCooldown, cd);
    }

    private void TickAttackLock(NPCBlackboard bb, float dt)
    {
        if (!bb.TryGetValue<float>(FSAIBlackboardKeys.AttackLockTimer, out var lockTimer, EntityManager))
            return;

        lockTimer -= dt;
        if (lockTimer <= 0f)
            ClearBreachTarget(bb);
        else
            bb.SetValue(FSAIBlackboardKeys.AttackLockTimer, lockTimer);
    }

    private void CheckBreachInterrupts(EntityUid uid, HTNComponent htn, NPCBlackboard bb)
    {
        if (!bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.BreachTarget, out var breachTarget, EntityManager))
            return;

        if (!Exists(breachTarget)
            || (TryComp<MobStateComponent>(breachTarget, out _) && _mobState.IsDead(breachTarget)))
        {
            ClearBreachTarget(bb);
            _htn.Replan(htn);
            return;
        }

        if (!_evaluator.IsBlockingMovement(breachTarget))
        {
            ClearBreachTarget(bb);
            _htn.Replan(htn);
            return;
        }

        if (!bb.TryGetValue<float>(FSAIBlackboardKeys.AttackLockTimer, out var lockVal, EntityManager)
            || lockVal <= 0f)
            return;

        var xform = Transform(uid);
        var worldPos = _transform.GetWorldPosition(xform);
        var mapId = xform.MapID;
        var epicenter = new MapCoordinates(worldPos, mapId);
        _mobBuffer.Clear();
        _lookup.GetEntitiesInRange(epicenter, 1.5f, _mobBuffer);
        foreach (var (nearUid, _) in _mobBuffer)
        {
            if (nearUid == uid) continue;
            if (HasComp<WaveSpawnedTagComponent>(nearUid)) continue;
            if (HasComp<GhostComponent>(nearUid)) continue;
            if (_mobState.IsDead(nearUid)) continue;

            ClearBreachTarget(bb);
            _htn.Replan(htn);
            return;
        }

        if (!bb.TryGetValue<float>(FSAIBlackboardKeys.CachedBreachScore, out var cachedScore, EntityManager))
            return;

        var baseDamage = GetBaseZombieDamage();
        TryComp<FSBreachStateComponent>(uid, out var state);

        if (_evaluator.TryFindBetterCandidate(epicenter, uid, breachTarget, baseDamage, worldPos, mapId,
                state, _timing.CurTime, cachedScore, out var better, out var betterScore))
        {
            if (state != null) FSBreachEvaluator.RecordSelection(state, better, _timing.CurTime);
            bb.SetValue(FSAIBlackboardKeys.BreachTarget, better);
            bb.SetValue(FSAIBlackboardKeys.AttackLockTimer, 15f);
            bb.SetValue(FSAIBlackboardKeys.CachedBreachScore, betterScore);
            _htn.Replan(htn);
        }
    }

    private void ClearBreachTarget(NPCBlackboard bb)
    {
        bb.Remove<EntityUid>(FSAIBlackboardKeys.BreachTarget);
        bb.Remove<float>(FSAIBlackboardKeys.AttackLockTimer);
        bb.Remove<float>(FSAIBlackboardKeys.CachedBreachScore);
        bb.Remove<int>(FSAIBlackboardKeys.BreachEvalFailCount);
        bb.Remove<Vector2>(FSAIBlackboardKeys.LastPathProgress);
        bb.SetValue(FSAIBlackboardKeys.PathProgressTimer, 0f);
        bb.SetValue(FSAIBlackboardKeys.BreachCooldown, 1.5f);
    }

    private const float ProgressSampleInterval = 0.6f;
    private const float MinClearProgress = 0.4f;
    private const float MazeCheckInterval = 2.0f;
    // Path length / direct-distance ratio. Straight corridor ≈ 1.0, zig-zag maze ≈ 2.0-2.2, normal L-turns ≈ 1.3-1.5.
    private const float MazePathRatio = 1.8f;
    private const int MazeMinPathCount = 5;
    private const float MazeAttackLockTime = 5f;

    private void CheckMazeBreach(EntityUid uid, EntityUid target, HTNComponent htn, NPCBlackboard bb, float dt)
    {
        if (!TryComp<NPCSteeringComponent>(uid, out var steering) || steering.CurrentPath.Count < MazeMinPathCount)
            return;

        var zombiePos = _transform.GetWorldPosition(uid);
        var targetPos = _transform.GetWorldPosition(target);
        var directDist = Vector2.Distance(zombiePos, targetPos);

        if (directDist < 2f)
            return;

        if (steering.CurrentPath.Count < directDist * MazePathRatio)
            return;

        var mapId = Transform(uid).MapID;
        var zombieEpicenter = new MapCoordinates(zombiePos, mapId);
        _doorBuffer.Clear();
        _lookup.GetEntitiesInRange(zombieEpicenter, 2.5f, _doorBuffer);
        if (_doorBuffer.Count > 0)
            return;

        var timer = bb.TryGetValue<float>(FSAIBlackboardKeys.MazeCheckTimer, out var mt, EntityManager) ? mt + dt : dt;
        bb.SetValue(FSAIBlackboardKeys.MazeCheckTimer, timer);
        if (timer < MazeCheckInterval)
            return;
        bb.SetValue(FSAIBlackboardKeys.MazeCheckTimer, 0f);

        ApplyEvaluation(uid, htn, bb, zombiePos, mapId, mazeTargetPos: targetPos);
    }

    private void CheckPathProgress(EntityUid uid, HTNComponent htn, NPCBlackboard bb, float dt)
    {
        if (bb.ContainsKey(FSAIBlackboardKeys.BreachTarget)) return;
        if (bb.ContainsKey(FSAIBlackboardKeys.AttackLockTimer)) return;
        if (bb.ContainsKey(FSAIBlackboardKeys.BreachCooldown)) return;
        if (bb.TryGetValue<EntityUid>("Target", out var target, EntityManager)
            && Exists(target) && !_mobState.IsDead(target))
        {
            CheckMazeBreach(uid, target, htn, bb, dt);
            return;
        }
        if (TryComp<PhysicsComponent>(uid, out var physComp)
            && physComp.LinearVelocity.LengthSquared() > 0.25f)
        {
            bb.SetValue(FSAIBlackboardKeys.LastPathProgress, _transform.GetWorldPosition(uid));
            bb.SetValue(FSAIBlackboardKeys.PathProgressTimer, 0f);
            return;
        }

        var hasActivePath = TryComp<NPCSteeringComponent>(uid, out var steerCheck) && steerCheck.CurrentPath.Count > 0;
        if (!hasActivePath)
        {
            var zeroTimer = bb.TryGetValue<float>(FSAIBlackboardKeys.PathZeroTimer, out var zt, EntityManager) ? zt + dt : dt;
            bb.SetValue(FSAIBlackboardKeys.PathZeroTimer, zeroTimer);
            if (zeroTimer < 0.5f)
            {
                bb.SetValue(FSAIBlackboardKeys.LastPathProgress, _transform.GetWorldPosition(uid));
                bb.SetValue(FSAIBlackboardKeys.PathProgressTimer, 0f);
                return;
            }
        }
        else
        {
            bb.Remove<float>(FSAIBlackboardKeys.PathZeroTimer);
        }

        var sampleTimer = bb.TryGetValue<float>(FSAIBlackboardKeys.PathProgressTimer, out var t, EntityManager) ? t + dt : dt;
        bb.SetValue(FSAIBlackboardKeys.PathProgressTimer, sampleTimer);

        if (sampleTimer < ProgressSampleInterval)
            return;

        bb.SetValue(FSAIBlackboardKeys.PathProgressTimer, 0f);

        var currentPos = _transform.GetWorldPosition(uid);

        if (!bb.TryGetValue<Vector2>(FSAIBlackboardKeys.LastPathProgress, out var baselinePos, EntityManager))
        {
            bb.SetValue(FSAIBlackboardKeys.LastPathProgress, currentPos);
            return;
        }

        if (Vector2.Distance(currentPos, baselinePos) >= MinClearProgress)
        {
            bb.SetValue(FSAIBlackboardKeys.LastPathProgress, currentPos);
            bb.SetValue(FSAIBlackboardKeys.BreachEvalFailCount, 0);
        }
        else
        {
            var mapId = Transform(uid).MapID;
            var epicenter = new MapCoordinates(currentPos, mapId);
            _doorBuffer.Clear();
            _lookup.GetEntitiesInRange(epicenter, 1.5f, _doorBuffer);
            if (_doorBuffer.Count > 0)
            {
                bb.SetValue(FSAIBlackboardKeys.LastPathProgress, currentPos);
                bb.SetValue(FSAIBlackboardKeys.PathProgressTimer, 0f);
                return;
            }
            ApplyEvaluation(uid, htn, bb, currentPos, mapId);
        }
    }

    // Applies FSBreachEvaluator's result to the blackboard; fail-count tracking is stall-mode only.
    private void ApplyEvaluation(EntityUid zombie, HTNComponent htn, NPCBlackboard bb,
        Vector2 zombieWorldPos, MapId mapId, Vector2? mazeTargetPos = null)
    {
        var baseDamage = GetBaseZombieDamage();
        var isMazeMode = mazeTargetPos.HasValue;
        TryComp<FSBreachStateComponent>(zombie, out var state);

        var failCount = 0;
        if (!isMazeMode)
            failCount = bb.TryGetValue<int>(FSAIBlackboardKeys.BreachEvalFailCount, out var fc, EntityManager) ? fc : 0;

        var result = _evaluator.EvaluateBreachTarget(zombie, zombieWorldPos, mapId, baseDamage, state, isMazeMode, failCount, mazeTargetPos);

        if (result == null)
        {
            if (!isMazeMode)
                bb.SetValue(FSAIBlackboardKeys.BreachEvalFailCount, failCount + 1);
            return;
        }

        if (!isMazeMode)
            bb.SetValue(FSAIBlackboardKeys.BreachEvalFailCount, 0);
        bb.SetValue(FSAIBlackboardKeys.BreachTarget, result.Value.Best);
        bb.SetValue(FSAIBlackboardKeys.AttackLockTimer, isMazeMode ? MazeAttackLockTime : 15f);
        bb.SetValue(FSAIBlackboardKeys.CachedBreachScore, result.Value.Score);
        _htn.Replan(htn);
    }

    private float GetBaseZombieDamage()
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent>();
        while (query.MoveNext(out _, out var comp))
            return comp.BaseZombieMeleeDamage;
        return _baseZombieDamage;
    }
}
