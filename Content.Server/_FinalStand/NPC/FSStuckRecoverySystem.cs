// Recovers wave zombies that stop making progress: nudges the boxed-in, relocates the stranded.
using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Content.Shared.NPC;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.NPC;

public sealed partial class FSStuckRecoverySystem : EntitySystem
{
    [Dependency] private HordeFlowFieldSystem _flow = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private record struct StuckState(Vector2 LastPos, TimeSpan LastMoveTime, TimeSpan LastNudge, int NudgeCount, TimeSpan NoPathSince);
    private readonly Dictionary<EntityUid, StuckState> _state = new();

    private readonly List<EntityUid> _removeBuffer = new();
    private readonly List<EntityCoordinates> _spawnerBuffer = new();
    private readonly HashSet<EntityUid> _entBuffer = new();
    private readonly Dictionary<Vector2i, int> _neighborCrowd = new();

    private bool _enabled;
    private int _nudgeLimit;
    private const float StuckDistance = 1f;
    private const float StuckSeconds = 20f;
    private const float NudgeCooldown = 8f;
    private const float PurgeInterval = 30f;
    private const float NoPathGraceSeconds = 5f;

    // Stuck detection works on a 20s horizon, so sampling faster than 1 Hz buys nothing.
    private const float TickInterval = 1f;

    private float _accumulator;
    private float _purgeTimer;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(NPCSteeringSystem));
        Subs.CVar(_cfg, CCVars.WaveZombiePathingEnabled, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.WaveZombieStuckNudgeLimit, v => _nudgeLimit = v, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_enabled)
            return;

        _accumulator += frameTime;
        if (_accumulator < TickInterval)
            return;
        _accumulator -= TickInterval;

        var curTime = _timing.CurTime;

        _purgeTimer += TickInterval;
        if (_purgeTimer >= PurgeInterval)
        {
            _purgeTimer = 0f;
            _removeBuffer.Clear();
            foreach (var uid in _state.Keys)
            {
                if (!Exists(uid))
                    _removeBuffer.Add(uid);
            }
            foreach (var uid in _removeBuffer)
                _state.Remove(uid);
        }

        var query = EntityQueryEnumerator<ActiveNPCComponent, WaveSpawnedTagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out _, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(xform);

            if (!_state.TryGetValue(uid, out var s))
            {
                _state[uid] = new StuckState(worldPos, curTime, TimeSpan.Zero, 0, TimeSpan.Zero);
                continue;
            }

            // Pathfinder gave up: if NoPath persists and the flow field says the tile is unreachable, relocate.
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
        if (xform.GridUid is not { } gridUid)
            return false;

        return _flow.IsReachable(gridUid, HordeFlowFieldSystem.ToTile(xform.LocalPosition));
    }

    private void TryRelocateStranded(EntityUid uid, TransformComponent xform)
    {
        _spawnerBuffer.Clear();
        var spawnerQuery = EntityQueryEnumerator<WaveEnemySpawnerComponent, TransformComponent>();
        while (spawnerQuery.MoveNext(out _, out _, out var spXform))
        {
            if (spXform.GridUid is not { } spGrid)
                continue;
            if (_flow.IsReachable(spGrid, HordeFlowFieldSystem.ToTile(spXform.LocalPosition)))
                _spawnerBuffer.Add(spXform.Coordinates);
        }

        if (_spawnerBuffer.Count == 0)
        {
            Log.Info($"[FSStuckRecovery] Deleting stranded {ToPrettyString(uid)}: no reachable spawner");
            QueueDel(uid);
            return;
        }

        _transform.SetCoordinates(uid, _random.Pick(_spawnerBuffer));
        ResetSteering(uid);
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
        var myTile = HordeFlowFieldSystem.ToTile(localPos);
        BuildNeighborCrowd(gridUid, myTile);

        EntityCoordinates? goalCoords = null;
        if (TryComp<NPCSteeringComponent>(uid, out var steering))
            goalCoords = steering.Coordinates;

        var myDistToGoal = float.MaxValue;
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

                _neighborCrowd.TryGetValue(neighborTile, out var count);
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
        ResetSteering(uid);

        var newWorldPos = _transform.GetWorldPosition(xform);
        s = s with { LastPos = newWorldPos, LastMoveTime = curTime, LastNudge = curTime, NudgeCount = s.NudgeCount + 1 };
        Log.Info($"[FSStuckRecovery] Nudged wave zombie {ToPrettyString(uid)} from {myTile} (nudge {s.NudgeCount}/{_nudgeLimit}).");
    }

    private void ResetSteering(EntityUid uid)
    {
        if (!TryComp<NPCSteeringComponent>(uid, out var steering))
            return;

        steering.Status = SteeringStatus.Moving;
        steering.CurrentPath.Clear();
        steering.FailedPathCount = 0;
        steering.PathfindToken?.Cancel();
        steering.PathfindToken = null;
    }

    // Counts wave zombies per surrounding tile so the nudge prefers the least crowded step out.
    private void BuildNeighborCrowd(EntityUid gridUid, Vector2i myTile)
    {
        _neighborCrowd.Clear();
        _entBuffer.Clear();
        var center = new Vector2(myTile.X + 0.5f, myTile.Y + 0.5f);
        _lookup.GetLocalEntitiesIntersecting(gridUid, Box2.CenteredAround(center, new Vector2(4f, 4f)), _entBuffer);

        foreach (var ent in _entBuffer)
        {
            if (!HasComp<WaveSpawnedTagComponent>(ent))
                continue;

            var entXform = Transform(ent);
            if (entXform.GridUid != gridUid)
                continue;

            var tile = HordeFlowFieldSystem.ToTile(entXform.LocalPosition);
            _neighborCrowd.TryGetValue(tile, out var count);
            _neighborCrowd[tile] = count + 1;
        }
    }

    private bool IsTileWalkable(EntityUid gridUid, Vector2 center, int ourLayer, int ourMask)
    {
        _entBuffer.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, Box2.CenteredAround(center, new Vector2(0.9f, 0.9f)), _entBuffer, LookupFlags.Static);

        foreach (var ent in _entBuffer)
        {
            if (!TryComp<PhysicsComponent>(ent, out var body) || !body.Hard || !body.CanCollide)
                continue;
            if ((body.CollisionLayer & ourMask) != 0 || (body.CollisionMask & ourLayer) != 0)
                return false;
        }
        return true;
    }
}
