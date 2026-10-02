// Where a placeable may go. Shared so the client ghost and the server confirm cannot disagree.
using Content.Shared._FinalStand.RCD;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._FinalStand.Placement;

public sealed class FSPlacementRuleSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private readonly HashSet<Entity<MobStateComponent>> _mobs = new();

    public bool CanPlaceAt(EntityUid user, EntityCoordinates coords, FSPlaceableComponent comp)
    {
        if (!_transform.InRange(Transform(user).Coordinates, coords, comp.Range))
            return false;

        return _turf.TryGetTileRef(coords, out var tile)
               && !_turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable)
               && IsTileBuildable(coords, false);
    }

    public bool IsTileBuildable(EntityCoordinates coords, bool blocksPath)
    {
        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var tile = _map.TileIndicesFor(gridUid, grid, coords);

        if (blocksPath)
        {
            var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, tile);
            while (anchored.MoveNext(out var ent))
            {
                if (HasComp<FSNoRCDBuildComponent>(ent))
                    return false;
            }
        }

        _mobs.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid, tile, _mobs, flags: LookupFlags.Dynamic);
        foreach (var mob in _mobs)
        {
            if (!_mobState.IsDead(mob, mob.Comp))
                return false;
        }

        return true;
    }
}
