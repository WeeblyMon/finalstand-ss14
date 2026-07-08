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
using Robust.Shared.Prototypes;
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

    private const float TickInterval = 0.1f;
    private float _accumulator;
    private float _baseZombieDamage = 10f;

    private static readonly EntProtoId ZombieNormalProto = "FSZombieNormal";

    private static readonly TimeSpan SelectionWindow = TimeSpan.FromSeconds(15);
    private const int BlacklistThreshold = 3;

    // Radius around an opening door in which we force nearby zombies to repath. The cached A*
    // path was built when the door was closed, so it likely detours through an adjacent door —
    // forcing a replan picks up the now-open route. L4D2-style "navmesh dirty" event.
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

    // When a door opens or is destroyed, nearby zombies have cached paths that route around it
    // through whatever adjacent obstacle A* picked instead. Forcing a replan rebuilds the path
    // through the newly-cleared tile so rear zombies don't commit to smashing an adjacent door.
    private void OnDoorStateChanged(EntityUid doorUid, DoorComponent door, ref DoorStateChangedEvent args)
    {
        if (args.State != DoorState.Open)
            return;
        ForceNearbyReplan(doorUid);
    }

    // Covers the smash case: when front zombies melee a door to death, the entity is deleted
    // (no Open state transition fires). ComponentShutdown catches the deletion so rear zombies
    // still replan through the now-clear tile.
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
            // Drop any in-flight breach lock — the route ahead may have opened up.
            if (TryComp<HTNComponent>(npcUid, out var htn))
            {
                if (htn.Blackboard.ContainsKey(FSAIBlackboardKeys.BreachTarget))
                    ClearBreachTarget(htn.Blackboard);
                _htn.Replan(htn);
            }
            // Clear cached path so the next steering tick requests a fresh one through the cleared tile.
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
        TickAttackLock(bb, dt);
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


    private void TickAttackLock(NPCBlackboard bb, float dt)
    {
        if (!bb.TryGetValue<float>(FSAIBlackboardKeys.AttackLockTimer, out var lockTimer, EntityManager))
            return;

        lockTimer -= dt;
        if (lockTimer <= 0f)
            ClearBreachTarget(bb); // clear so zombie re-evaluates route rather than backtracking
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

    private const float ProgressSampleInterval = 0.6f;
    private const float MinClearProgress = 0.4f;
    private const float MazeCheckInterval = 2.0f;
    // Path waypoints / direct-tile distance. A straight corridor ≈ 1.0; a zig-zag railing
    // maze of 3 railings produces ~2.0–2.2. 1.8 catches that while leaving normal L-turns
    // (~1.3–1.5) unaffected. The scoring step (MazeMinScore) is the real filter for walls.
    private const float MazePathRatio = 1.8f;
    // Don't trigger on trivially short paths (noise / turn-rounding).
    private const int MazeMinPathCount = 5;
    // Minimum score in maze mode. With shortcut geometry: railing (cost≈1-10) scores ≥0.07;
    // regular wall (cost≈50) scores 0.014. Threshold of 0.015 lets railings through and
    // keeps structures needing 47+ zombie hits out. Intentionally NOT failCount-adaptive —
    // maze mode must never escalate to targeting proper walls.
    private const float MazeMinScore = 0.015f;
    // Shorter attack lock for maze breaches — releases quickly if the front of the horde
    // clears the route (e.g. opens an airlock) before the zombie finishes its breach.
    private const float MazeAttackLockTime = 5f;

    // Compare nav-path length against direct distance to target.
    // If the ratio exceeds MazePathRatio, check whether it's cheaper to breach
    // nearby structures than to follow the maze. The score formula handles the
    // rest: cheap barriers (railings) score high; expensive ones (walls) fall below
    // minScore and are ignored, so zombies correctly run wall mazes but smash railing ones.
    private void CheckMazeBreach(EntityUid uid, EntityUid target, HTNComponent htn, NPCBlackboard bb, float dt)
    {
        if (!TryComp<NPCSteeringComponent>(uid, out var steering) || steering.CurrentPath.Count < MazeMinPathCount)
            return;

        var zombiePos = _transform.GetWorldPosition(uid);
        var targetPos = _transform.GetWorldPosition(target);
        var directDist = Vector2.Distance(zombiePos, targetPos);

        // Avoid divide-by-zero / false positives when already adjacent.
        if (directDist < 2f)
            return;

        if (steering.CurrentPath.Count < directDist * MazePathRatio)
            return;

        // Don't trigger when near an airlock — the HTN pry/smash task handles doors,
        // and the path-ratio spikes temporarily as the horde queues at the airlock.
        // Using 2.5f (wider than stuck detection's 1.5f) to also catch zombies a step
        // behind the group that are still "in the airlock zone." Timer is not advanced
        // while suppressed, so evaluation starts fresh once the zombie moves clear.
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
        if (bb.ContainsKey(FSAIBlackboardKeys.BreachTarget)) return;
        if (bb.ContainsKey(FSAIBlackboardKeys.AttackLockTimer)) return;
        if (bb.ContainsKey(FSAIBlackboardKeys.BreachCooldown)) return;
        if (bb.TryGetValue<EntityUid>("Target", out var target, EntityManager)
            && Exists(target) && !_mobState.IsDead(target))
        {
            // Anti-maze: compare path length to direct distance. If the route is
            // 2.5× longer than the straight line, evaluate nearby structures —
            // cheap ones (railings) will be targeted; expensive ones (walls) score
            // too low and are skipped, so zombies still run wall mazes naturally.
            CheckMazeBreach(uid, target, htn, bb, dt);
            return;
        }
        // Velocity gate: if the zombie is physically moving it is not stuck — don't evaluate breach.
        // Position sampling alone can fire on baseline-reset artifacts even while the zombie is
        // navigating normally. Checking physics velocity is the ground truth.
        if (TryComp<PhysicsComponent>(uid, out var physComp)
            && physComp.LinearVelocity.LengthSquared() > 0.25f) // > ~0.5 tiles/s
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

        // Maze mode uses a fixed threshold — never relax it to avoid targeting real walls.
        // Stuck mode uses the adaptive failCount threshold to break stall loops.
        float minScore;
        int failCount = 0;
        if (isMazeMode)
        {
            minScore = MazeMinScore;
        }
        else
        {
            failCount = bb.TryGetValue<int>(FSAIBlackboardKeys.BreachEvalFailCount, out var fc, EntityManager) ? fc : 0;
            minScore = failCount >= 5 ? 0.005f : 0.01f;
        }

        if (best == EntityUid.Invalid || score < minScore)
        {
            if (!isMazeMode)
                bb.SetValue(FSAIBlackboardKeys.BreachEvalFailCount, failCount + 1);
            return;
        }
        if (state != null)
        {
            RecordSelection(state, best, curTime);
            if (state.Blacklist.ContainsKey(best))
            {
                if (!isMazeMode)
                    bb.SetValue(FSAIBlackboardKeys.BreachEvalFailCount, failCount + 1);
                return;
            }
        }

        if (!isMazeMode)
            bb.SetValue(FSAIBlackboardKeys.BreachEvalFailCount, 0);
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
            // Maze mode: filter candidates toward the actual target, not the nav waypoint.
            // Nav waypoint direction is the maze route direction — using it would filter out
            // shortcut structures that happen to be perpendicular to the current corridor.
            var toTarget = mazeTargetPos.Value - zombieWorldPos;
            if (toTarget.LengthSquared() > 0.01f)
            {
                travelDir = Vector2.Normalize(toTarget);
                hasTravelDir = true;
            }
        }
        else
        {
            // Stuck mode: filter toward next nav waypoint (original behaviour).
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
            if (HasComp<DoorComponent>(candidate)) continue; // doors handled by pry/smash system
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
                ? ScoreCandidateMaze(candidate, zombie, baseDamage, zombieWorldPos, mazeTargetPos.Value)
                : ScoreCandidate(candidate, zombie, baseDamage, zombieWorldPos, mapId);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return (best, bestScore);
    }

    // ── Blacklist helpers

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

    private bool IsBlockingMovement(EntityUid entity)
    {
        // Unanchored entities (glass shards, ammo casings, dropped items) have Hard = true
        // by default in Robust Toolbox but sit on SlipLayer/ItemLayer — mobs walk right
        // through them. Only anchored/static structures are real barriers.
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
            if (fixture.Hard)
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
        var baseScore = (weakness * 0.3f + navValue * 0.7f) / cost;
        var attackers = CountZombiesAttackingTarget(structure, zombie);
        return baseScore * (1f + MathF.Min(attackers, 5) * 0.2f);
    }

    // Maze scoring: replaces GetNavValue with shortcut geometry.
    // A structure's "shortcut value" is how well it sits on the direct zombie→target line.
    // Structures directly in the way score 1.0; 4+ tiles off the line score 0.2 (still non-zero
    // so even off-axis barriers are considered if they're cheap enough).
    private float ScoreCandidateMaze(EntityUid structure, EntityUid zombie, float baseDamage,
        Vector2 zombiePos, Vector2 targetPos)
    {
        var weakness = GetWeakness(structure);
        var shortcut = GetShortcutValue(structure, zombiePos, targetPos);
        var cost = GetBreachCost(structure, baseDamage);
        var baseScore = (weakness * 0.3f + shortcut * 0.7f) / cost;
        var attackers = CountZombiesAttackingTarget(structure, zombie);
        return baseScore * (1f + MathF.Min(attackers, 5) * 0.2f);
    }

    private int CountZombiesAttackingTarget(EntityUid target, EntityUid exclude)
    {
        var count = 0;
        var query = EntityQueryEnumerator<WaveSpawnedTagComponent, HTNComponent>();
        while (query.MoveNext(out var uid, out _, out var htn))
        {
            if (uid == exclude) continue;
            if (htn.Blackboard.TryGetValue<EntityUid>(FSAIBlackboardKeys.BreachTarget, out var bt, EntityManager) && bt == target)
                count++;
        }
        return count;
    }

    // Projects the structure's position onto the direct zombie→target line and measures
    // lateral deviation. Returns 0 for structures behind the zombie (dot ≤ 0).
    private float GetShortcutValue(EntityUid structure, Vector2 zombiePos, Vector2 targetPos)
    {
        var structPos = _transform.GetWorldPosition(structure);
        var toTarget = targetPos - zombiePos;
        var targetDist = toTarget.Length();
        if (targetDist < 0.01f)
            return 0.2f;
        var targetDir = toTarget / targetDist;
        var toStruct = structPos - zombiePos;
        // Structures behind the zombie are never a shortcut.
        if (Vector2.Dot(toStruct, targetDir) <= 0f)
            return 0f;
        // Lateral distance from the direct zombie→target line (cross product magnitude).
        var lateral = MathF.Abs(toStruct.X * targetDir.Y - toStruct.Y * targetDir.X);
        // Full score (1.0) on the line; decays linearly to 0.2 at 4 tiles off the line.
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
            // 0.6f threshold: adjacent tiles are 1.0f apart, so only entities ON the waypoint
            // tile (path routing through them) qualify. Entities next to an open waypoint don't.
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
