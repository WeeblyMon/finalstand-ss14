// Where a placeable may go. Shared so the client ghost and the server confirm cannot disagree.
using Content.Shared.Maps;
using Content.Shared.Physics;
using Robust.Shared.Map;

namespace Content.Shared._FinalStand.Placement;

public sealed class FSPlacementRuleSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    public bool CanPlaceAt(EntityUid user, EntityCoordinates coords, FSPlaceableComponent comp)
    {
        if (!_transform.InRange(Transform(user).Coordinates, coords, comp.Range))
            return false;

        return _turf.TryGetTileRef(coords, out var tile)
               && !_turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable);
    }
}
