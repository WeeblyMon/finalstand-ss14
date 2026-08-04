// Tile-connectivity map from the CCCs. IsReachable(tile) tells consumers whether a zombie
// can ever walk to an objective, so genuinely stranded zombies can be relocated.
using System.Numerics;
using Content.Server._FinalStand.Station;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Content.Shared.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._FinalStand.NPC;

public sealed class HordeFlowFieldSystem : EntitySystem
{
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private readonly HashSet<(EntityUid, Vector2i)> _reachable = new();
    private readonly Queue<Vector2i> _frontier = new();
    private readonly HashSet<Vector2i> _visited = new();
    private readonly Dictionary<EntityUid, List<Vector2i>> _seedsByGrid = new();

    private float _rebuildTimer;
    private bool _enabled;
    private bool _dirty = true;
    private bool _truncated;

    private const float RebuildInterval = 5f;

    // Safety valve only. A station grid is a few thousand tiles; anything past this means the
    // fill escaped onto something unbounded, so the result is discarded rather than trusted.
    private const int MaxTilesPerGrid = 100_000;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(NPCSteeringSystem));
        Subs.CVar(_cfg, CCVars.HordeBrainEnabled, v => _enabled = v, true);

        // Only tile topology and the CCC set can change reachability. Walls and doors do not:
        // wave zombies smash and pry, so a barrier is a detour, never a dead end.
        SubscribeLocalEvent<TileChangedEvent>(OnTileChanged);
        SubscribeLocalEvent<FinalStandCCCComponent, ComponentStartup>(OnCCCStartup);
        SubscribeLocalEvent<FinalStandCCCComponent, ComponentShutdown>(OnCCCShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (!_enabled || !_dirty)
            return;

        _rebuildTimer += frameTime;
        if (_rebuildTimer < RebuildInterval)
            return;

        _rebuildTimer = 0f;
        _dirty = false;
        Rebuild();
    }

    private void OnTileChanged(ref TileChangedEvent args) => MarkDirty();

    private void OnCCCStartup(EntityUid uid, FinalStandCCCComponent comp, ComponentStartup args) => MarkDirty();

    private void OnCCCShutdown(EntityUid uid, FinalStandCCCComponent comp, ComponentShutdown args) => MarkDirty();

    public bool IsReachable(EntityUid gridUid, Vector2i tile) => _reachable.Contains((gridUid, tile));

    /// <summary>Force a rebuild on the next interval (e.g. hull breach, CCC moved).</summary>
    public void MarkDirty() => _dirty = true;

    /// <summary>False while the field is empty or untrustworthy — callers must not act on it.</summary>
    public bool HasField => !_truncated && _reachable.Count > 0;

    private void Rebuild()
    {
        _reachable.Clear();
        _seedsByGrid.Clear();
        _truncated = false;

        var cccQuery = EntityQueryEnumerator<FinalStandCCCComponent, TransformComponent>();
        while (cccQuery.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid is not { } gridUid)
                continue;

            if (!_seedsByGrid.TryGetValue(gridUid, out var seeds))
                _seedsByGrid[gridUid] = seeds = new List<Vector2i>();

            seeds.Add(ToTile(xform.LocalPosition));
        }

        foreach (var (gridUid, seeds) in _seedsByGrid)
            FloodFill(gridUid, seeds);
    }

    // Breadth-first fill over non-space tiles. Each tile is tested exactly once.
    private void FloodFill(EntityUid gridUid, List<Vector2i> seeds)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return;

        _frontier.Clear();
        _visited.Clear();

        foreach (var seed in seeds)
        {
            if (_visited.Add(seed))
                _frontier.Enqueue(seed);
        }

        while (_frontier.TryDequeue(out var current))
        {
            if (_visited.Count > MaxTilesPerGrid)
            {
                _truncated = true;
                Log.Error($"[HordeFlowField] Fill on grid {gridUid} exceeded {MaxTilesPerGrid} tiles — field discarded.");
                return;
            }

            _reachable.Add((gridUid, current));

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    var neighbor = new Vector2i(current.X + dx, current.Y + dy);
                    if (!_visited.Add(neighbor))
                        continue;

                    if (IsWalkable(gridUid, mapGrid, neighbor))
                        _frontier.Enqueue(neighbor);
                }
            }
        }
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
