using Content.Shared._FinalStand.CCC;
using System.Numerics;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._FinalStand.NPC;

public sealed partial class HordeFlowFieldSystem : EntitySystem
{
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private HashSet<(EntityUid, Vector2i)> _reachable = new();
    private HashSet<(EntityUid, Vector2i)> _building = new();
    private readonly Queue<Vector2i> _frontier = new();
    private readonly HashSet<Vector2i> _visited = new();
    private readonly Dictionary<EntityUid, List<Vector2i>> _seedsByGrid = new();
    private readonly List<EntityUid> _gridQueue = new();

    private EntityUid? _fillGrid;
    private MapGridComponent? _fillGridComp;
    private bool _fillTruncated;

    private float _rebuildTimer;
    private bool _enabled;
    private bool _dirty = true;
    private bool _truncated;

    private const float RebuildInterval = 5f;

    private const int MaxTilesPerGrid = 100_000;

    private const int TilesPerTick = 2500;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(NPCSteeringSystem));
        Subs.CVar(_cfg, CCVars.WaveZombiePathingEnabled, v => _enabled = v, true);

        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<FinalStandCCCComponent, ComponentStartup>(OnCCCStartup);
        SubscribeLocalEvent<FinalStandCCCComponent, ComponentShutdown>(OnCCCShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_enabled)
            return;

        if (IsFilling)
        {
            StepFill(TilesPerTick);
            return;
        }

        if (!_dirty)
            return;

        _rebuildTimer += frameTime;
        if (_rebuildTimer < RebuildInterval)
            return;

        _rebuildTimer = 0f;
        _dirty = false;
        BeginRebuild();
    }

    private bool IsFilling => _fillGrid != null || _gridQueue.Count > 0;

    private void OnTileChanged(ref TileChangedEvent args) => MarkDirty();

    private void OnCCCStartup(EntityUid uid, FinalStandCCCComponent comp, ComponentStartup args) => MarkDirty();

    private void OnCCCShutdown(EntityUid uid, FinalStandCCCComponent comp, ComponentShutdown args) => MarkDirty();

    public bool IsReachable(EntityUid gridUid, Vector2i tile) => _reachable.Contains((gridUid, tile));

    /// <summary>Force a rebuild on the next interval (e.g. hull breach, CCC moved).</summary>
    public void MarkDirty() => _dirty = true;

    /// <summary>False while the field is empty or untrustworthy; callers must not act on it.</summary>
    public bool HasField => !_truncated && _reachable.Count > 0;

    private void BeginRebuild()
    {
        _building.Clear();
        _seedsByGrid.Clear();
        _gridQueue.Clear();
        _fillGrid = null;
        _fillGridComp = null;
        _fillTruncated = false;

        var cccQuery = EntityQueryEnumerator<FinalStandCCCComponent, TransformComponent>();
        while (cccQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid is not { } gridUid)
                continue;

            if (!_seedsByGrid.TryGetValue(gridUid, out var seeds))
            {
                _seedsByGrid[gridUid] = seeds = new List<Vector2i>();
                _gridQueue.Add(gridUid);
            }

            seeds.Add(ToTile(xform.LocalPosition));
        }

        if (_gridQueue.Count == 0)
            PublishField();
    }

    // Breadth-first fill over non-space tiles. Each tile is tested exactly once.
    private void StepFill(int budget)
    {
        while (budget > 0)
        {
            if (_fillGrid is not { } grid || _fillGridComp is not { } gridComp)
            {
                if (TryStartNextGrid())
                    continue;

                PublishField();
                return;
            }

            if (!_frontier.TryDequeue(out var current))
            {
                EndGrid();
                continue;
            }

            if (_visited.Count > MaxTilesPerGrid)
            {
                _fillTruncated = true;
                Log.Error($"Fill on grid {grid} exceeded {MaxTilesPerGrid} tiles - field discarded.");
                EndGrid();
                continue;
            }

            budget--;
            _building.Add((grid, current));

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var neighbor = new Vector2i(current.X + dx, current.Y + dy);
                    if (!_visited.Add(neighbor))
                        continue;

                    if (IsWalkable(grid, gridComp, neighbor))
                        _frontier.Enqueue(neighbor);
                }
            }
        }
    }

    private void EndGrid()
    {
        _frontier.Clear();
        _fillGrid = null;
        _fillGridComp = null;
    }

    private bool TryStartNextGrid()
    {
        while (_gridQueue.Count > 0)
        {
            var gridUid = _gridQueue[^1];
            _gridQueue.RemoveAt(_gridQueue.Count - 1);

            if (!TryComp<MapGridComponent>(gridUid, out var mapGrid)
                || !_seedsByGrid.TryGetValue(gridUid, out var seeds))
                continue;

            _fillGrid = gridUid;
            _fillGridComp = mapGrid;
            _frontier.Clear();
            _visited.Clear();

            foreach (var seed in seeds)
            {
                if (_visited.Add(seed))
                    _frontier.Enqueue(seed);
            }

            return true;
        }

        return false;
    }

    private void PublishField()
    {
        (_reachable, _building) = (_building, _reachable);
        _truncated = _fillTruncated;
        _building.Clear();
        _frontier.Clear();
        _visited.Clear();
    }

    private bool IsWalkable(EntityUid gridUid, MapGridComponent mapGrid, Vector2i tile)
    {
        return _mapSystem.TryGetTileRef(gridUid, mapGrid, tile, out var tileRef)
               && !tileRef.Tile.IsEmpty
               && !_turf.IsSpace(tileRef);
    }

    public static Vector2i ToTile(Vector2 localPos) =>
        new((int)MathF.Floor(localPos.X), (int)MathF.Floor(localPos.Y));
}
