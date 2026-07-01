// Evaluates and assigns breach targets for wave zombies blocked on their path to the CCC.
using System.Numerics;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.NPC;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible.Thresholds.Triggers;
using Content.Shared.FixedPoint;
using Content.Shared.GameTicking.Components;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Doors;
using Content.Shared.Doors.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Content.Shared.Physics;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

public sealed class FSBreachTargetSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speedMod = default!;
    [Dependency] private readonly HordeFlowFieldSystem _flow = default!;

    private const float SpeedJitterMin = 0.85f;
    private const float SpeedJitterMax = 1.15f;
    private const float SpawnGraceDuration = 5f;

    private const float TickInterval = 0.1f;
    private float _accumulator;
    private float _baseZombieDamage = 10f;

    private static readonly EntProtoId ZombieNormalProto = "FSZombieNormal";

    private static readonly TimeSpan SelectionWindow = TimeSpan.FromSeconds(15);
    private const int BlacklistThreshold = 3;

    private const float DoorOpenReplanRadius = 6f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, MapInitEvent>(OnWaveEnemyStartup);
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

    private void OnWaveEnemyStartup(EntityUid uid, WaveSpawnedTagComponent _, MapInitEvent args)
    {
        EnsureComp<FSBreachStateComponent>(uid);

        if (TryComp<HTNComponent>(uid, out var htn))
        {
            htn.Blackboard.SetValue(FSAIBlackboardKeys.SpawnGrace, SpawnGraceDuration);
            htn.Blackboard.SetValue(FSAIBlackboardKeys.ApproachAngle, _random.NextFloat(0f, MathF.Tau));
        }

        if (TryComp<MovementSpeedModifierComponent>(uid, out var speedComp))
        {
            var mult = _random.NextFloat(SpeedJitterMin, SpeedJitterMax);
            _speedMod.ChangeBaseSpeed(uid,
                speedComp.BaseWalkSpeed * mult,
                speedComp.BaseSprintSpeed * mult,
                speedComp.Acceleration,
                speedComp);
        }
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
        _flow.MarkDirty();
        if (args.State != DoorState.Open)
            return;
        ForceNearbyReplan(doorUid);
    }

    private void OnDoorShutdown(EntityUid doorUid, DoorComponent door, ComponentShutdown args)
    {
        _flow.MarkDirty();
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


    public void TryUpdateRetaliationState(EntityUid uid, EntityUid attacker, TimeSpan curTime)
    {
        if (!HasComp<MobStateComponent>(attacker)) return;
        if (!Exists(attacker) || _mobState.IsDead(attacker)) return;
        if (HasComp<WaveSpawnedTagComponent>(attacker)) return;
        if (!TryComp<HTNComponent>(uid, out var htn)) return;

        htn.Blackboard.SetValue(FSAIBlackboardKeys.LastAttacker, attacker);
        htn.Blackboard.SetValue(FSAIBlackboardKeys.RetaliationTimer, 2f);
        htn.Blackboard.SetValue("FSAggroGraceUntil", curTime + TimeSpan.FromSeconds(2));
    }


    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < TickInterval) return;
        _accumulator -= TickInterval;
        var query = EntityQueryEnumerator<WaveSpawnedTagComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out _, out var htn))
            Tick(uid, htn, TickInterval);
    }

    private void Tick(EntityUid uid, HTNComponent htn, float dt)
    {
        var bb = htn.Blackboard;
        TickRetaliation(uid, htn, bb, dt);
        TickAttackLock(htn, bb, dt);
        TickBreachCooldown(bb, dt);
        CheckBreachInterrupts(uid, htn, bb);
        CheckPathProgress(uid, htn, bb, dt);
    }

    private void TickRetaliation(EntityUid uid, HTNComponent htn, NPCBlackboard bb, float dt)
    {
        if (bb.TryGetValue<float>(FSAIBlackboardKeys.RetaliationTimer, out var retTimer, EntityManager))
        {
            retTimer -= dt;
            if (retTimer <= 0f)
            {
                bb.Remove<float>(FSAIBlackboardKeys.RetaliationTimer);
                if (bb.ContainsKey(FSAIBlackboardKeys.LastAttacker))
                    bb.Remove<EntityUid>(FSAIBlackboardKeys.LastAttacker);
                return;
            }
            bb.SetValue(FSAIBlackboardKeys.RetaliationTimer, retTimer);
        }

        if (bb.TryGetValue<EntityUid>(FSAIBlackboardKeys.LastAttacker, out var attacker, EntityManager)
            && (!Exists(attacker) || _mobState.IsDead(attacker)))
        {
            bb.Remove<EntityUid>(FSAIBlackboardKeys.LastAttacker);
            bb.Remove<float>(FSAIBlackboardKeys.RetaliationTimer);
            _htn.Replan(htn);
        }
    }


    private void TickBreachCooldown(NPCBlackboard bb, float dt)
    {
        if (!bb.TryGetValue<float>(FSAIBlackboardKeys.BreachCooldown, out var cd, EntityManager)) return;
        cd -= dt;
        if (cd <= 0f) bb.Remove<float>(FSAIBlackboardKeys.BreachCooldown);
        else bb.SetValue(FSAIBlackboardKeys.BreachCooldown, cd);
    }


    private void TickAttackLock(HTNComponent htn, NPCBlackboard bb, float dt)
    {
        if (!bb.TryGetValue<float>(FSAIBlackboardKeys.AttackLockTimer, out var lockTimer, EntityManager))
            return;

        lockTimer -= dt;
        if (lockTimer <= 0f)
        {
            ClearBreachTarget(bb);
            _htn.Replan(htn);
        }
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

        if (!IsBlockingMovement(breachTarget))
        {
            ClearBreachTarget(bb);
            _htn.Replan(htn);
            return;
        }

        if (!bb.TryGetValue<float>(FSAIBlackboardKeys.AttackLockTimer, out var lockVal, EntityManager)
            || lockVal <= 0f)
            return;

        var worldPos = _transform.GetWorldPosition(uid);
        var mapId = Transform(uid).MapID;
        var epicenter = new MapCoordinates(worldPos, mapId);
        var nearbyMobs = new HashSet<Entity<MobStateComponent>>();
        _lookup.GetEntitiesInRange<MobStateComponent>(epicenter, 1.5f, nearbyMobs);
        foreach (var (nearUid, _) in nearbyMobs)
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
        var destructibles = new HashSet<Entity<DestructibleComponent>>();
        _lookup.GetEntitiesInRange<DestructibleComponent>(epicenter, 4f, destructibles);

        TryComp<FSBreachStateComponent>(uid, out var state);
        var curTime = _timing.CurTime;

        foreach (var (candidate, _) in destructibles)
        {
            if (candidate == breachTarget) continue;
            if (HasComp<WaveSpawnedTagComponent>(candidate)) continue;
            if (HasComp<MobStateComponent>(candidate) && !_mobState.IsDead(candidate)) continue;
            if (!HasComp<DamageableComponent>(candidate)) continue;
            if (TryComp<FSBreachCostOverrideComponent>(candidate, out var ov) && ov.Cost >= 999f) continue;
            if (!IsBlockingMovement(candidate)) continue;
            if (state != null && state.Blacklist.TryGetValue(candidate, out var expiry) && curTime < expiry) continue;

            var score = ScoreCandidate(candidate, uid, baseDamage, worldPos, mapId);
            if (score > cachedScore * 2f)
            {
                if (state != null) RecordSelection(state, candidate, curTime);
                bb.SetValue(FSAIBlackboardKeys.BreachTarget, candidate);
                bb.SetValue(FSAIBlackboardKeys.AttackLockTimer, 15f);
                bb.SetValue(FSAIBlackboardKeys.CachedBreachScore, score);
                _htn.Replan(htn);
                return;
            }
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

    private const float ProgressSampleInterval = 2.0f;
    private const float MinClearProgress = 0.4f;
    private const float MazeCheckInterval = 4.0f;
    private const float MazePathRatio = 2.5f;
    private const int MazeMinPathCount = 5;
    private const float MazeMinScore = 0.015f;
    private const float MazeAttackLockTime = 5f;
    private const float PhysicsStuckThreshold = 4f;

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
        var nearDoors = new HashSet<Entity<DoorComponent>>();
        _lookup.GetEntitiesInRange<DoorComponent>(zombieEpicenter, 2.5f, nearDoors);
        if (nearDoors.Count > 0)
            return;

        var timer = bb.TryGetValue<float>(FSAIBlackboardKeys.MazeCheckTimer, out var mt, EntityManager) ? mt + dt : dt;
        bb.SetValue(FSAIBlackboardKeys.MazeCheckTimer, timer);
        if (timer < MazeCheckInterval)
            return;
        bb.SetValue(FSAIBlackboardKeys.MazeCheckTimer, 0f);

        EvaluateBreachTarget(uid, htn, bb, zombiePos, mapId, mazeTargetPos: targetPos);
    }

    private void CheckPathProgress(EntityUid uid, HTNComponent htn, NPCBlackboard bb, float dt)
    {
        if (bb.TryGetValue<float>(FSAIBlackboardKeys.SpawnGrace, out var grace, EntityManager))
        {
            grace -= dt;
            if (grace > 0f) { bb.SetValue(FSAIBlackboardKeys.SpawnGrace, grace); return; }
            bb.Remove<float>(FSAIBlackboardKeys.SpawnGrace);
        }

        if (bb.ContainsKey(FSAIBlackboardKeys.BreachTarget)) return;
        if (bb.ContainsKey(FSAIBlackboardKeys.AttackLockTimer)) return;
        if (bb.ContainsKey(FSAIBlackboardKeys.BreachCooldown)) return;
        if (bb.TryGetValue<EntityUid>("Target", out var target, EntityManager)
            && Exists(target) && !_mobState.IsDead(target))
        {
            CheckMazeBreach(uid, target, htn, bb, dt);
            return;
        }

        var hasVelocity = TryComp<PhysicsComponent>(uid, out var physComp)
            && physComp.LinearVelocity.LengthSquared() > 0.25f;
        var hasIntendedMovement = TryComp<InputMoverComponent>(uid, out var mover)
            && mover.CurTickSprintMovement != Vector2.Zero;
        if (hasVelocity)
        {
            bb.SetValue(FSAIBlackboardKeys.LastPathProgress, _transform.GetWorldPosition(uid));
            bb.SetValue(FSAIBlackboardKeys.PathProgressTimer, 0f);
            bb.Remove<float>(FSAIBlackboardKeys.PhysicsStuckTimer);
            return;
        }

        // Zombie wants to move but has no velocity — may be physically stuck against an obstacle.
        // Use a separate timer so brief crowd jostling doesn't trigger breach evaluation.
        if (hasIntendedMovement)
        {
            var stuckTimer = bb.TryGetValue<float>(FSAIBlackboardKeys.PhysicsStuckTimer, out var st, EntityManager) ? st + dt : dt;
            if (stuckTimer < PhysicsStuckThreshold)
            {
                bb.SetValue(FSAIBlackboardKeys.PhysicsStuckTimer, stuckTimer);
                return;
            }
            // Stuck for long enough — clear timer and fall through to breach evaluation
            bb.Remove<float>(FSAIBlackboardKeys.PhysicsStuckTimer);
            var stuckPos = _transform.GetWorldPosition(uid);
            var stuckMapId = Transform(uid).MapID;
            var stuckEpicenter = new MapCoordinates(stuckPos, stuckMapId);
            var stuckDoors = new HashSet<Entity<DoorComponent>>();
            _lookup.GetEntitiesInRange<DoorComponent>(stuckEpicenter, 1.5f, stuckDoors);
            if (stuckDoors.Count == 0)
                EvaluateBreachTarget(uid, htn, bb, stuckPos, stuckMapId);
            return;
        }
        bb.Remove<float>(FSAIBlackboardKeys.PhysicsStuckTimer);

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
            var doorNearby = new HashSet<Entity<DoorComponent>>();
            _lookup.GetEntitiesInRange<DoorComponent>(epicenter, 1.5f, doorNearby);
            if (doorNearby.Count > 0)
            {
                bb.SetValue(FSAIBlackboardKeys.LastPathProgress, currentPos);
                bb.SetValue(FSAIBlackboardKeys.PathProgressTimer, 0f);
                return;
            }
            EvaluateBreachTarget(uid, htn, bb, currentPos, mapId);
        }
    }

    private void EvaluateBreachTarget(EntityUid zombie, HTNComponent htn, NPCBlackboard bb,
        Vector2 zombieWorldPos, MapId mapId, Vector2? mazeTargetPos = null)
    {
        var baseDamage = GetBaseZombieDamage();
        var epicenter = new MapCoordinates(zombieWorldPos, mapId);
        var curTime = _timing.CurTime;
        var isMazeMode = mazeTargetPos.HasValue;

        TryComp<FSBreachStateComponent>(zombie, out var state);
        if (state != null)
            PruneExpiredBlacklist(state, curTime);

        var (best, score) = BestInRange(epicenter, 4f, zombie, baseDamage, zombieWorldPos, mapId, state, curTime, mazeTargetPos);

        if (best == EntityUid.Invalid)
            (best, score) = BestInRange(epicenter, 6f, zombie, baseDamage, zombieWorldPos, mapId, state, curTime, mazeTargetPos);

        var minScore = isMazeMode ? MazeMinScore : 0.05f;

        if (best == EntityUid.Invalid || score < minScore)
            return;
        if (state != null)
        {
            RecordSelection(state, best, curTime);
            if (state.Blacklist.ContainsKey(best))
                return;
        }

        bb.SetValue(FSAIBlackboardKeys.BreachTarget, best);
        bb.SetValue(FSAIBlackboardKeys.AttackLockTimer, isMazeMode ? MazeAttackLockTime : 15f);
        bb.SetValue(FSAIBlackboardKeys.CachedBreachScore, score);
        _htn.Replan(htn);
    }

    private (EntityUid Candidate, float Score) BestInRange(MapCoordinates epicenter, float radius,
        EntityUid zombie, float baseDamage, Vector2 zombieWorldPos, MapId mapId,
        FSBreachStateComponent? state, TimeSpan curTime, Vector2? mazeTargetPos = null)
    {
        var destructibles = new HashSet<Entity<DestructibleComponent>>();
        _lookup.GetEntitiesInRange<DestructibleComponent>(epicenter, radius, destructibles);

        var travelDir = Vector2.Zero;
        var hasTravelDir = false;

        if (mazeTargetPos.HasValue)
        {
            var toTarget = mazeTargetPos.Value - zombieWorldPos;
            if (toTarget.LengthSquared() > 0.01f)
            {
                travelDir = Vector2.Normalize(toTarget);
                hasTravelDir = true;
            }
        }
        else
        {
            if (TryComp<NPCSteeringComponent>(zombie, out var steeringComp) &&
                steeringComp.CurrentPath.TryPeek(out var nextPoly))
            {
                var waypointPos = _transform.ToMapCoordinates(nextPoly.Coordinates).Position;
                var toWaypoint = waypointPos - zombieWorldPos;
                if (toWaypoint.LengthSquared() > 0.01f)
                {
                    travelDir = Vector2.Normalize(toWaypoint);
                    hasTravelDir = true;
                }
            }
            if (!hasTravelDir)
            {
                var facing = _transform.GetWorldRotation(zombie).ToVec();
                if (facing.LengthSquared() > 0.01f)
                {
                    travelDir = facing;
                    hasTravelDir = true;
                }
            }
        }

        EntityUid best = EntityUid.Invalid;
        float bestScore = -1f;

        foreach (var (candidate, _) in destructibles)
        {
            if (HasComp<WaveSpawnedTagComponent>(candidate)) continue;
            if (HasComp<DoorComponent>(candidate)) continue;
            if (HasComp<MobStateComponent>(candidate) && !_mobState.IsDead(candidate)) continue;
            if (!HasComp<DamageableComponent>(candidate)) continue;
            if (TryComp<FSBreachCostOverrideComponent>(candidate, out var ov) && ov.Cost >= 999f) continue;
            if (!IsBlockingMovement(candidate)) continue;
            if (IsAdjacentToSpace(candidate)) continue;
            if (state != null && state.Blacklist.TryGetValue(candidate, out var expiry) && curTime < expiry) continue;
            if (hasTravelDir)
            {
                var toCandidate = _transform.GetWorldPosition(candidate) - zombieWorldPos;
                if (toCandidate.LengthSquared() > 0.01f &&
                    Vector2.Dot(travelDir, Vector2.Normalize(toCandidate)) < -0.5f)
                    continue;
            }

            var score = mazeTargetPos.HasValue
                ? ScoreCandidateMaze(candidate, baseDamage, zombieWorldPos, mazeTargetPos.Value)
                : ScoreCandidate(candidate, zombie, baseDamage, zombieWorldPos, mapId);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return (best, bestScore);
    }

    private static void PruneExpiredBlacklist(FSBreachStateComponent state, TimeSpan curTime)
    {
        List<EntityUid>? toRemove = null;
        foreach (var (uid, expiry) in state.Blacklist)
        {
            if (curTime >= expiry)
                (toRemove ??= new List<EntityUid>()).Add(uid);
        }
        if (toRemove == null) return;
        foreach (var uid in toRemove)
            state.Blacklist.Remove(uid);
    }

    private static void RecordSelection(FSBreachStateComponent state, EntityUid target, TimeSpan curTime)
    {
        if (state.SelectionHistory.TryGetValue(target, out var entry))
        {
            if (curTime - entry.FirstSelected < SelectionWindow)
            {
                var newCount = entry.Count + 1;
                if (newCount >= BlacklistThreshold)
                {
                    state.Blacklist[target] = curTime + SelectionWindow;
                    state.SelectionHistory.Remove(target);
                    return;
                }
                state.SelectionHistory[target] = (newCount, entry.FirstSelected);
            }
            else
            {
                state.SelectionHistory[target] = (1, curTime);
            }
        }
        else
        {
            state.SelectionHistory[target] = (1, curTime);
        }
    }


    private static readonly Vector2i[] CardinalOffsets =
        [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    private bool IsAdjacentToSpace(EntityUid entity)
    {
        var xform = Transform(entity);
        if (xform.GridUid is not { } gridUid) return false;
        if (!TryComp<MapGridComponent>(gridUid, out var gridComp)) return false;

        var indices = _mapSystem.GetTileRef(gridUid, gridComp, xform.Coordinates).GridIndices;

        foreach (var offset in CardinalOffsets)
        {
            if (!_mapSystem.TryGetTileRef(gridUid, gridComp, indices + offset, out var tile)
                || tile.Tile.IsEmpty
                || _turf.IsSpace(tile))
                return true;
        }
        return false;
    }

    private const int MobBlockLayers = (int)(CollisionGroup.Impassable | CollisionGroup.HighImpassable
                                            | CollisionGroup.MidImpassable | CollisionGroup.LowImpassable);

    private bool IsBlockingMovement(EntityUid entity)
    {
        if (!Transform(entity).Anchored)
            return false;
        if (!TryComp<PhysicsComponent>(entity, out var physics))
            return false;
        if (!physics.CanCollide)
            return false;
        if (!TryComp<FixturesComponent>(entity, out var fixtures))
            return false;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (fixture.Hard && (fixture.CollisionLayer & MobBlockLayers) != 0)
                return true;
        }
        return false;
    }

    private float ScoreCandidate(EntityUid structure, EntityUid zombie, float baseDamage,
        Vector2 zombieWorldPos, MapId mapId)
    {
        var weakness = GetWeakness(structure);
        var navValue = GetNavValue(structure, zombie, zombieWorldPos, mapId);
        var cost = GetBreachCost(structure, baseDamage);
        return (weakness * 0.3f + navValue * 0.7f) / cost;
    }

    private float ScoreCandidateMaze(EntityUid structure, float baseDamage,
        Vector2 zombiePos, Vector2 targetPos)
    {
        var weakness = GetWeakness(structure);
        var shortcut = GetShortcutValue(structure, zombiePos, targetPos);
        var cost = GetBreachCost(structure, baseDamage);
        return (weakness * 0.3f + shortcut * 0.7f) / cost;
    }

    private float GetShortcutValue(EntityUid structure, Vector2 zombiePos, Vector2 targetPos)
    {
        var structPos = _transform.GetWorldPosition(structure);
        var toTarget = targetPos - zombiePos;
        var targetDist = toTarget.Length();
        if (targetDist < 0.01f)
            return 0.2f;
        var targetDir = toTarget / targetDist;
        var toStruct = structPos - zombiePos;
        if (Vector2.Dot(toStruct, targetDir) <= 0f)
            return 0f;
        var lateral = MathF.Abs(toStruct.X * targetDir.Y - toStruct.Y * targetDir.X);
        var t = MathF.Max(0f, 1f - lateral / 4f);
        return 0.2f + 0.8f * t;
    }

    private float GetBreachCost(EntityUid structure, float baseDamage)
    {
        if (TryComp<FSBreachCostOverrideComponent>(structure, out var overrideComp))
            return overrideComp.Cost;

        return MathF.Max(1f, GetStructureMaxHp(structure) / baseDamage);
    }

    private float GetStructureMaxHp(EntityUid structure)
    {
        if (HasComp<MobThresholdsComponent>(structure)
            && _mobThreshold.TryGetThresholdForState(structure, MobState.Dead, out var deadThreshold)
            && deadThreshold.HasValue)
            return deadThreshold.Value.Float();

        if (TryComp<DestructibleComponent>(structure, out var destructible))
        {
            var maxHp = 0f;
            foreach (var threshold in destructible.Thresholds)
            {
                if (threshold.Trigger is DamageTrigger dt)
                    maxHp = MathF.Max(maxHp, dt.Damage.Float());
            }
            if (maxHp > 0f)
                return maxHp;
        }

        return 100f;
    }

    private float GetWeakness(EntityUid structure)
    {
        if (!HasComp<DamageableComponent>(structure)) return 0f;
        if (!HasComp<MobThresholdsComponent>(structure)) return 0f;
        if (!_mobThreshold.TryGetThresholdForState(structure, MobState.Dead, out var deadThreshold)
            || !deadThreshold.HasValue || deadThreshold.Value <= FixedPoint2.Zero)
            return 0f;

        var totalDamage = _damageable.GetTotalDamage((structure, null));
        var ratio = totalDamage.Float() / deadThreshold.Value.Float();
        return MathF.Max(0f, MathF.Min(1f, ratio));
    }

    private float GetNavValue(EntityUid structure, EntityUid zombie, Vector2 zombieWorldPos, MapId mapId)
    {
        if (!TryComp<NPCSteeringComponent>(zombie, out var steering) || steering.CurrentPath.Count == 0)
            return 0.2f;
        var structurePos = _transform.GetWorldPosition(structure);
        var count = 0;
        foreach (var poly in steering.CurrentPath)
        {
            if (count >= 3) break;
            var mapPos = _transform.ToMapCoordinates(poly.Coordinates);
            if ((structurePos - mapPos.Position).Length() <= 0.6f)
                return 1.0f;
            count++;
        }
        return 0.2f;
    }

    private float GetBaseZombieDamage()
    {
        var query = EntityQueryEnumerator<WaveGameRuleComponent>();
        while (query.MoveNext(out _, out var comp))
            return comp.BaseZombieMeleeDamage;
        return _baseZombieDamage;
    }
}
