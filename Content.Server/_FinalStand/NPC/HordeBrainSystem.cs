// Per-tile zombie occupancy snapshot rebuilt before NPCSteeringSystem runs.
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.Systems;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.NPC;

public sealed class HordeBrainSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly Dictionary<(EntityUid, Vector2i), int> _occupancy = new();
    private readonly Dictionary<(EntityUid, Vector2i), float> _smoothedOccupancy = new();

    private bool _enabled;
    private int _occupancyThreshold;
    private int _flowTrigger;
    private float _flowWeight;

    public int OccupancyThreshold => _occupancyThreshold;
    public int FlowTrigger => _flowTrigger;
    public float FlowWeight => _flowWeight;
    public bool IsEnabled => _enabled;

    private float _debugTimer;
    private const float DebugLogInterval = 10f;

    public override void Initialize()
    {
        base.Initialize();
        UpdatesBefore.Add(typeof(NPCSteeringSystem));

        Subs.CVar(_cfg, CCVars.HordeBrainEnabled, v =>
        {
            _enabled = v;
            Log.Warning($"[HordeBrain] Enabled = {v}");
        }, true);
        Subs.CVar(_cfg, CCVars.HordeBrainOccupancyThreshold, v => _occupancyThreshold = v, true);
        Subs.CVar(_cfg, CCVars.HordeBrainFlowTrigger, v => _flowTrigger = v, true);
        Subs.CVar(_cfg, CCVars.HordeBrainFlowWeight, v => _flowWeight = v, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _debugTimer += frameTime;
        if (_debugTimer >= DebugLogInterval)
        {
            _debugTimer = 0f;
            Log.Warning($"[HordeBrain] Heartbeat: enabled={_enabled}, occupiedTiles={_occupancy.Count}");
            if (_enabled)
                LogPeakTile();
        }

        if (!_enabled)
        {
            _occupancy.Clear();
            _smoothedOccupancy.Clear();
            return;
        }

        RebuildSnapshot();
    }

    private void RebuildSnapshot()
    {
        _occupancy.Clear();
        _smoothedOccupancy.Clear();

        var query = EntityQueryEnumerator<WaveSpawnedTagComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.GridUid is not { } gridUid)
                continue;

            var localPos = xform.LocalPosition;
            var tile = new Vector2i((int)MathF.Floor(localPos.X), (int)MathF.Floor(localPos.Y));

            _occupancy.TryGetValue((gridUid, tile), out var current);
            _occupancy[(gridUid, tile)] = current + 1;

            AddSmoothed(gridUid, tile, 1.0f);
            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    AddSmoothed(gridUid, new Vector2i(tile.X + dx, tile.Y + dy), 0.5f);
                }
            }
        }
    }

    private void AddSmoothed(EntityUid gridUid, Vector2i tile, float weight)
    {
        _smoothedOccupancy.TryGetValue((gridUid, tile), out var current);
        _smoothedOccupancy[(gridUid, tile)] = current + weight;
    }

    public int GetOccupancy(EntityUid gridUid, Vector2i tile)
    {
        if (!_enabled)
            return 0;

        _occupancy.TryGetValue((gridUid, tile), out var count);
        return count;
    }

    public float GetSmoothedOccupancy(EntityUid gridUid, Vector2i tile)
    {
        if (!_enabled)
            return 0f;

        _smoothedOccupancy.TryGetValue((gridUid, tile), out var count);
        return count;
    }

    private void LogPeakTile()
    {
        if (_occupancy.Count == 0)
        {
            Log.Info("[HordeBrain] Snapshot empty — no wave zombies on any grid.");
            return;
        }

        var peak = (Key: default((EntityUid, Vector2i)), Count: 0);
        foreach (var (key, count) in _occupancy)
        {
            if (count > peak.Count)
                peak = (key, count);
        }

        Log.Info($"[HordeBrain] Snapshot: {_occupancy.Count} occupied tiles, " +
                    $"peak={peak.Count} zombies at tile {peak.Key.Item2} on grid {peak.Key.Item1}.");
    }
}
