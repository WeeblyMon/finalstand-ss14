using Content.Server.Fluids.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Maps;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSSplashFlaskSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSplashFlaskComponent, LandEvent>(OnLand);
    }

    private void OnLand(Entity<FSSplashFlaskComponent> ent, ref LandEvent args)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution)
            || solution.Volume <= 0)
        {
            return;
        }

        var mapCoords = _xform.GetMapCoordinates(ent);

        if (!_map.TryFindGridAt(mapCoords, out var gridUid, out var grid)
            || !_map.TryGetTileRef(gridUid, grid, Transform(ent).Coordinates, out var tileRef)
            || _turf.IsSpace(tileRef))
        {
            return;
        }

        var payload = _solutions.SplitSolution(soln.Value, solution.Volume);
        var coords = _map.MapToGrid(gridUid, mapCoords);
        var cloud = Spawn(ent.Comp.CloudProto, coords.SnapToGrid());

        _smoke.StartSmoke(cloud, payload, ent.Comp.Duration, ent.Comp.SpreadAmount);

        QueueDel(ent);
    }
}
