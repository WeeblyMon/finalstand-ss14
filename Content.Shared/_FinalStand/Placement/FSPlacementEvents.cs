using Robust.Shared.Map;

namespace Content.Shared._FinalStand.Placement;

// Raised on the placeable item once a click-to-place has passed range validation.
public sealed class FSPlacementConfirmedEvent(EntityUid user, EntityCoordinates coordinates) : HandledEntityEventArgs
{
    public readonly EntityUid User = user;
    public readonly EntityCoordinates Coordinates = coordinates;
}
