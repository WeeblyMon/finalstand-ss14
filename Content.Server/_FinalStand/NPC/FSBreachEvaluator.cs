using System.Numerics;
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
using Content.Shared.Maps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC;
using Content.Shared.Doors.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

// Finds and scores breach-target candidates for FSBreachTargetSystem.
public sealed partial class FSBreachEvaluator : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _mobThreshold = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private IGameTiming _timing = default!;

    // Reused across ticks — these queries run at 10 Hz per zombie and must not allocate.
    private readonly HashSet<Entity<DestructibleComponent>> _destructibleBuffer = new();
    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _peerBuffer = new();

    private readonly Dictionary<EntityUid, int> _attackerTally = new();

    private const float AttackerTallyRadius = 8f;

    private static readonly TimeSpan SelectionWindow = TimeSpan.FromSeconds(15);
    private const int BlacklistThreshold = 3;

    private static readonly Vector2i[] CardinalOffsets =
        [new(1, 0), new(-1, 0), new(0, 1), new(0, -1)];

    public void BuildAttackerTally(MapCoordinates epicenter, EntityUid exclude)
    {
        _attackerTally.Clear();
        _peerBuffer.Clear();
        _lookup.GetEntitiesInRange(epicenter, AttackerTallyRadius, _peerBuffer);

        foreach (var (peer, _) in _peerBuffer)
        {
            if (peer == exclude) continue;
            if (!TryComp<HTNComponent>(peer, out var peerHtn)) continue;
            if (!peerHtn.Blackboard.TryGetValue<EntityUid>(FSAIBlackboardKeys.BreachTarget, out var bt, EntityManager))
                continue;

            _attackerTally.TryGetValue(bt, out var count);
            _attackerTally[bt] = count + 1;
        }
    }

    public float ScoreCandidate(EntityUid structure, EntityUid zombie, float baseDamage,
        Vector2 zombieWorldPos, MapId mapId)
    {
        var weakness = GetWeakness(structure);
        var navValue = GetNavValue(structure, zombie, zombieWorldPos, mapId);
        var cost = GetBreachCost(structure, baseDamage);
        var baseScore = (weakness * 0.3f + navValue * 0.7f) / cost;
        return baseScore * PackBonus(structure);
    }

    public float ScoreCandidateMaze(EntityUid structure, float baseDamage,
        Vector2 zombiePos, Vector2 targetPos)
    {
        var weakness = GetWeakness(structure);
        var shortcut = GetShortcutValue(structure, zombiePos, targetPos);
        var cost = GetBreachCost(structure, baseDamage);
        var baseScore = (weakness * 0.3f + shortcut * 0.7f) / cost;
        return baseScore * PackBonus(structure);
    }

    public bool TryFindBetterCandidate(MapCoordinates epicenter, EntityUid zombie, EntityUid currentTarget,
        float baseDamage, Vector2 zombieWorldPos, MapId mapId, FSBreachStateComponent? state,
        TimeSpan curTime, float cachedScore, out EntityUid better, out float betterScore)
    {
        BuildAttackerTally(epicenter, zombie);
        _destructibleBuffer.Clear();
        _lookup.GetEntitiesInRange(epicenter, 4f, _destructibleBuffer);

        foreach (var (candidate, _) in _destructibleBuffer)
        {
            if (candidate == currentTarget) continue;
            if (HasComp<WaveSpawnedTagComponent>(candidate)) continue;
            if (HasComp<MobStateComponent>(candidate) && !_mobState.IsDead(candidate)) continue;
            if (!HasComp<DamageableComponent>(candidate)) continue;
            if (TryComp<FSBreachCostOverrideComponent>(candidate, out var ov) && ov.Cost >= 999f) continue;
            if (!IsBlockingMovement(candidate)) continue;
            if (state != null && state.Blacklist.TryGetValue(candidate, out var expiry) && curTime < expiry) continue;

            var score = ScoreCandidate(candidate, zombie, baseDamage, zombieWorldPos, mapId);
            if (score > cachedScore * 2f)
            {
                better = candidate;
                betterScore = score;
                return true;
            }
        }

        better = EntityUid.Invalid;
        betterScore = 0f;
        return false;
    }

    public (EntityUid Candidate, float Score) BestInRange(MapCoordinates epicenter, float radius,
        EntityUid zombie, float baseDamage, Vector2 zombieWorldPos, MapId mapId,
        FSBreachStateComponent? state, TimeSpan curTime, Vector2? mazeTargetPos = null)
    {
        _destructibleBuffer.Clear();
        _lookup.GetEntitiesInRange(epicenter, radius, _destructibleBuffer);

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

        foreach (var (candidate, _) in _destructibleBuffer)
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

    public (EntityUid Best, float Score)? EvaluateBreachTarget(EntityUid zombie, Vector2 zombieWorldPos,
        MapId mapId, float baseDamage, FSBreachStateComponent? state, bool isMazeMode, int failCount,
        Vector2? mazeTargetPos = null)
    {
        var epicenter = new MapCoordinates(zombieWorldPos, mapId);
        var curTime = _timing.CurTime;

        BuildAttackerTally(epicenter, zombie);

        if (state != null)
            PruneExpiredBlacklist(state, curTime);

        var (best, score) = BestInRange(epicenter, 4f, zombie, baseDamage, zombieWorldPos, mapId, state, curTime, mazeTargetPos);

        if (best == EntityUid.Invalid)
            (best, score) = BestInRange(epicenter, 6f, zombie, baseDamage, zombieWorldPos, mapId, state, curTime, mazeTargetPos);

        var minScore = isMazeMode
            ? MazeMinScore
            : (failCount >= 5 ? 0.005f : 0.01f);

        if (best == EntityUid.Invalid || score < minScore)
            return null;

        if (state != null)
        {
            RecordSelection(state, best, curTime);
            if (state.Blacklist.ContainsKey(best))
                return null;
        }

        return (best, score);
    }

    public const float MazeMinScore = 0.015f;

    public static void PruneExpiredBlacklist(FSBreachStateComponent state, TimeSpan curTime)
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

    public static void RecordSelection(FSBreachStateComponent state, EntityUid target, TimeSpan curTime)
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

    public bool IsAdjacentToSpace(EntityUid entity)
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

    public bool IsBlockingMovement(EntityUid entity)
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
            if (fixture.Hard)
                return true;
        }
        return false;
    }

    private float PackBonus(EntityUid structure)
    {
        _attackerTally.TryGetValue(structure, out var attackers);
        return 1f + MathF.Min(attackers, 5) * 0.2f;
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
}
