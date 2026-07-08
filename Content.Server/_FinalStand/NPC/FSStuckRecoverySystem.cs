// Teleports genuinely stuck wave zombies to an adjacent tile.
// Also fast-paths zombies whose pathfinder gave up (SteeringStatus.NoPath) via a flow-field
// reachability check — stranded zombies get relocated to a reachable spawner instead of
// standing idle at their unreachable spawn tile until the 20s position-based check fires.
using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

public sealed class FSStuckRecoverySystem : EntitySystem
{
    [Dependency] private readonly HordeBrainSystem _hordeBrain = default!;
    [Dependency] private readonly HordeFlowFieldSystem _flow = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private record struct StuckState(Vector2 LastPos, TimeSpan LastMoveTime, TimeSpan LastNudge, int NudgeCount, TimeSpan NoPathSince);
    private readonly Dictionary<EntityUid, StuckState> _state = new();

    private bool _enabled;
    private int _nudgeLimit;
    private const float StuckDistance = 1f;
    private const float StuckSeconds = 20f;
    private const float NudgeCooldown = 8f;
    private const float PurgeInterval = 30f;
    private const float NoPathGraceSeconds = 5f;
    private float _purgeTimer;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(NPCSteeringSystem));
        Subs.CVar(_cfg, CCVars.HordeBrainEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.HordeBrainNudgeLimit, v => _nudgeLimit = v, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_enabled)
            return;

        var curTime = _timing.CurTime;

        _purgeTimer += frameTime;
        if (_purgeTimer >= PurgeInterval)
        {
            _purgeTimer = 0f;
            var toRemove = new List<EntityUid>();
            foreach (var uid in _state.Keys)
            {
                if (!Exists(uid))
                    toRemove.Add(uid);
            }
            foreach (var uid in toRemove)
                _state.Remove(uid);
        }

        var query = EntityQueryEnumerator<WaveSpawnedTagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(xform);

            if (!_state.TryGetValue(uid, out var s))
            {
                _state[uid] = new StuckState(worldPos, curTime, TimeSpan.Zero, 0, TimeSpan.Zero);
                continue;
            }

            // Fast path: pathfinder gave up. If they've been NoPath for a few seconds AND
            // the flow field says their tile is unreachable, relocate to a reachable spawner
            // (or delete if none exists).
            if (TryComp<NPCSteeringComponent>(uid, out var steering)
                && steering.Status == SteeringStatus.NoPath)
            {
                if (s.NoPathSince == TimeSpan.Zero)
                {
                    _state[uid] = s = s with { NoPathSince = curTime };
                }
                else if ((curTime - s.NoPathSince).TotalSeconds >= NoPathGraceSeconds
                         && _flow.HasField
                         && !IsFlowReachable(xform))
                {
                    TryRelocateStranded(uid, xform);
                    _state[uid] = s with { NoPathSince = TimeSpan.Zero, LastPos = _transform.GetWorldPosition(xform), LastMoveTime = curTime };
                    continue;
                }
            }
            else if (s.NoPathSince != TimeSpan.Zero)
            {
                _state[uid] = s = s with { NoPathSince = TimeSpan.Zero };
            }

            if ((worldPos - s.LastPos).LengthSquared() >= StuckDistance * StuckDistance)
            {
                _state[uid] = s with { LastPos = worldPos, LastMoveTime = curTime, NudgeCount = 0 };
                continue;
            }

            if ((curTime - s.LastMoveTime).TotalSeconds < StuckSeconds)
                continue;

            TryRecover(uid, xform, ref s, curTime);
            _state[uid] = s;
        }
    }

    private bool IsFlowReachable(TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid) return false;
        var localPos = xform.LocalPosition;
        var tile = new Vector2i((int)MathF.Floor(localPos.X), (int)MathF.Floor(localPos.Y));
        return _flow.IsReachable(gridUid, tile);
    }

    private void TryRelocateStranded(EntityUid uid, TransformComponent xform)
    {
        // Collect wave spawners whose tile is reachable per the flow field.
        var candidates = new List<EntityCoordinates>();
        var spawnerQuery = EntityQueryEnumerator<WaveEnemySpawnerComponent, TransformComponent>();
        while (spawnerQuery.MoveNext(out _, out _, out var spXform))
        {
            if (spXform.GridUid is not { } spGrid) continue;
            var sp = spXform.LocalPosition;
            var spTile = new Vector2i((int)MathF.Floor(sp.X), (int)MathF.Floor(sp.Y));
            if (_flow.IsReachable(spGrid, spTile))
                candidates.Add(spXform.Coordinates);
        }

        if (candidates.Count == 0)
        {
            Log.Info($"[FSStuckRecovery] Deleting stranded {ToPrettyString(uid)}: no reachable spawner");
            QueueDel(uid);
            return;
        }

        var target = _random.Pick(candidates);
        _transform.SetCoordinates(uid, target);

        if (TryComp<NPCSteeringComponent>(uid, out var steering))
        {
            steering.Status = SteeringStatus.Moving;
            steering.CurrentPath.Clear();
            steering.FailedPathCount = 0;
            steering.PathfindToken?.Cancel();
            steering.PathfindToken = null;
        }

        Log.Info($"[FSStuckRecovery] Relocated stranded {ToPrettyString(uid)} to reachable spawner.");
    }

    private void TryRecover(EntityUid uid, TransformComponent xform, ref StuckState s, TimeSpan curTime)
    {
        if (s.NudgeCount >= _nudgeLimit)
        {
            Log.Info($"[FSStuckRecovery] Deleting wave zombie {ToPrettyString(uid)} after {s.NudgeCount} nudges.");
            QueueDel(uid);
            return;
        }

        if ((curTime - s.LastNudge).TotalSeconds < NudgeCooldown)
            return;

        if (xform.GridUid is not { } gridUid)
        {
            Log.Info($"[FSStuckRecovery] Deleting wave zombie {ToPrettyString(uid)} — off-map.");
            QueueDel(uid);
            return;
        }

        var (ourLayer, ourMask) = _physics.GetHardCollision(uid);
        var localPos = xform.LocalPosition;
        var myTile = new Vector2i((int)MathF.Floor(localPos.X), (int)MathF.Floor(localPos.Y));

        EntityCoordinates? goalCoords = null;
        if (TryComp<NPCSteeringComponent>(uid, out var steering))
            goalCoords = steering.Coordinates;

        float myDistToGoal = float.MaxValue;
        if (goalCoords != null)
        {
            var zombieCoords = new EntityCoordinates(gridUid, localPos);
            zombieCoords.TryDistance(EntityManager, goalCoords.Value, out myDistToGoal);
        }

        EntityCoordinates? target = null;
        var bestCount = int.MaxValue;

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var neighborTile = new Vector2i(myTile.X + dx, myTile.Y + dy);
                var center = new Vector2(neighborTile.X + 0.5f, neighborTile.Y + 0.5f);

                if (goalCoords != null)
                {
                    var neighborCoords = new EntityCoordinates(gridUid, center);
                    if (neighborCoords.TryDistance(EntityManager, goalCoords.Value, out var nd) &&
                        nd < myDistToGoal - 0.25f)
                        continue;
                }

                var count = _hordeBrain.GetOccupancy(gridUid, neighborTile);
                if (count >= bestCount)
                    continue;

                if (!IsTileWalkable(gridUid, center, ourLayer, ourMask))
                    continue;

                bestCount = count;
                target = new EntityCoordinates(gridUid, center);
            }
        }

        if (target == null)
        {
            s = s with { LastNudge = TimeSpan.Zero, NudgeCount = s.NudgeCount + 1 };
            Log.Info($"[FSStuckRecovery] Wave zombie {ToPrettyString(uid)} at {myTile} fully boxed in (attempt {s.NudgeCount}/{_nudgeLimit}).");
            return;
        }

        _transform.SetCoordinates(uid, target.Value);

        if (steering != null)
        {
            steering.Status = SteeringStatus.Moving;
            steering.CurrentPath.Clear();
            steering.FailedPathCount = 0;
            steering.PathfindToken?.Cancel();
            steering.PathfindToken = null;
        }

        var newWorldPos = _transform.GetWorldPosition(xform);
        s = s with { LastPos = newWorldPos, LastMoveTime = curTime, LastNudge = curTime, NudgeCount = s.NudgeCount + 1 };
        Log.Info($"[FSStuckRecovery] Nudged wave zombie {ToPrettyString(uid)} from {myTile} (nudge {s.NudgeCount}/{_nudgeLimit}).");
    }

    private bool IsTileWalkable(EntityUid gridUid, Vector2 center, int ourLayer, int ourMask)
    {
        var box = Box2.CenteredAround(center, new Vector2(0.9f, 0.9f));
        var ents = new HashSet<EntityUid>();
        _lookup.GetLocalEntitiesIntersecting(gridUid, box, ents, LookupFlags.Static);

        foreach (var ent in ents)
        {
            if (!TryComp<PhysicsComponent>(ent, out var body) || !body.Hard || !body.CanCollide)
                continue;
            if ((body.CollisionLayer & ourMask) != 0 || (body.CollisionMask & ourLayer) != 0)
                return false;
        }
        return true;
    }
}
