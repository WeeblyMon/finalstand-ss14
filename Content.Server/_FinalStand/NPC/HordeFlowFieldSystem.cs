// Multi-source Dijkstra flow field from CCC seeds. Provides IsReachable(tile) queries
// so consumers (e.g. FSStuckRecoverySystem) can detect zombies stranded on tiles with
// no route to any CCC. Rebuild is amortised: dirty-flag based, capped by RebuildInterval.
using System.Collections.Generic;
using System.Numerics;
using Content.Server._FinalStand.Station;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Content.Shared.Doors.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;

namespace Content.Server._FinalStand.NPC;

public sealed class HordeFlowFieldSystem : EntitySystem
{
    [Dependency] private readonly HordeBrainSystem _hordeBrain = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly Dictionary<(EntityUid, Vector2i), Vector2> _flowDir = new();
    private readonly HashSet<(EntityUid, Vector2i)> _reachable = new();
    private readonly HashSet<EntityUid> _entBuffer = new();

    private float _rebuildTimer;
    private bool _enabled;
    private bool _dirty = true;

    private const float RebuildInterval = 5f;
    private const int MaxTilesPerGrid = 5000;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesAfter.Add(typeof(HordeBrainSystem));
        UpdatesBefore.Add(typeof(NPCSteeringSystem));
        Subs.CVar(_cfg, CCVars.HordeBrainEnabled, v => _enabled = v, true);
        // Door-state-change dirty ticks are triggered externally (FSBreachTargetSystem
        // owns the DoorStateChangedEvent subscription and calls MarkDirty here) so we
        // don't fight the event bus over duplicate subscriptions.
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_enabled) return;

        _rebuildTimer += frameTime;
        if (_rebuildTimer < RebuildInterval) return;
        _rebuildTimer = 0f;

        if (!_dirty) return;
        _dirty = false;
        Rebuild();
    }

    public Vector2 GetFlowDirection(EntityUid gridUid, Vector2i tile)
    {
        _flowDir.TryGetValue((gridUid, tile), out var dir);
        return dir;
    }

    public bool IsReachable(EntityUid gridUid, Vector2i tile) => _reachable.Contains((gridUid, tile));

    /// <summary>Force a rebuild on the next tick (e.g. wall destroyed, tile changed).</summary>
    public void MarkDirty() => _dirty = true;

    public bool HasField => _reachable.Count > 0;

    private void Rebuild()
    {
        _flowDir.Clear();
        _reachable.Clear();

        var seedsByGrid = new Dictionary<EntityUid, List<Vector2i>>();
        var cccQuery = EntityQueryEnumerator<FinalStandCCCComponent, TransformComponent>();
        while (cccQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid is not { } g)
                continue;

            var tile = new Vector2i(
                (int)MathF.Floor(xform.LocalPosition.X),
                (int)MathF.Floor(xform.LocalPosition.Y));

            if (!seedsByGrid.TryGetValue(g, out var list))
                seedsByGrid[g] = list = new List<Vector2i>();

            list.Add(tile);
        }

        foreach (var (gridUid, seeds) in seedsByGrid)
            BuildForGrid(gridUid, seeds);
    }

    private void BuildForGrid(EntityUid gridUid, List<Vector2i> seeds)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        var cost = new Dictionary<Vector2i, float>();
        var queue = new PriorityQueue<Vector2i, float>();

        foreach (var seed in seeds)
        {
            cost[seed] = 0f;
            queue.Enqueue(seed, 0f);
        }

        var expanded = 0;
        while (queue.Count > 0 && expanded < MaxTilesPerGrid)
        {
            queue.TryDequeue(out var current, out var currentCost);

            if (cost.TryGetValue(current, out var best) && best < currentCost - 0.001f)
                continue;

            expanded++;

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var neighbor = new Vector2i(current.X + dx, current.Y + dy);
                    var tileCost = GetTileCost(gridUid, mapGrid, neighbor);
                    if (tileCost < 0f)
                        continue;

                    var stepCost = dx != 0 && dy != 0 ? tileCost * 1.414f : tileCost;
                    var newCost = currentCost + stepCost;

                    if (cost.TryGetValue(neighbor, out var existing) && existing <= newCost + 0.001f)
                        continue;

                    cost[neighbor] = newCost;
                    queue.Enqueue(neighbor, newCost);
                }
            }
        }

        foreach (var (tile, _) in cost)
        {
            _reachable.Add((gridUid, tile));

            var bestDir = Vector2.Zero;
            var bestCost = float.MaxValue;

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var neighbor = new Vector2i(tile.X + dx, tile.Y + dy);
                    if (!cost.TryGetValue(neighbor, out var nc))
                        continue;

                    if (nc < bestCost)
                    {
                        bestCost = nc;
                        bestDir = new Vector2(dx, dy).Normalized();
                    }
                }
            }

            if (bestDir != Vector2.Zero)
                _flowDir[(gridUid, tile)] = bestDir;
        }
    }

    private float GetTileCost(EntityUid gridUid, MapGridComponent mapGrid, Vector2i tile)
    {
        if (!_mapSystem.TryGetTileRef(gridUid, mapGrid, tile, out var tileRef) || tileRef.Tile.IsEmpty)
            return -1f;

        var center = new Vector2(tile.X + 0.5f, tile.Y + 0.5f);
        var box = Box2.CenteredAround(center, new Vector2(0.85f, 0.85f));
        _entBuffer.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, box, _entBuffer, LookupFlags.Static);

        var hasDoor = false;
        var hasWall = false;

        foreach (var ent in _entBuffer)
        {
            if (!TryComp<PhysicsComponent>(ent, out var body) || !body.Hard || !body.CanCollide)
                continue;

            if (HasComp<DoorComponent>(ent))
                hasDoor = true;
            else
                hasWall = true;
        }

        float baseCost;
        if (hasWall)
            baseCost = 200f;
        else if (hasDoor)
            baseCost = 10f;
        else
            baseCost = 1f;

        baseCost += _hordeBrain.GetOccupancy(gridUid, tile) * 4f;
        return baseCost;
    }
}
