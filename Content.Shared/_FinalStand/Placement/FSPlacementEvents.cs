using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Placement;

// Raised on the placeable item once a click-to-place has passed range validation.
public sealed class FSPlacementConfirmedEvent(EntityUid user, EntityCoordinates coordinates, Direction direction) : HandledEntityEventArgs
{
    public readonly EntityUid User = user;
    public readonly EntityCoordinates Coordinates = coordinates;
    public readonly Direction Direction = direction;
}

// The ghost's rotation lives on the client, so it has to be relayed for the placed entity to match it.
[Serializable, NetSerializable]
public sealed class FSPlacementRotationMessage(NetEntity item, Direction direction) : EntityEventArgs
{
    public readonly NetEntity Item = item;
    public readonly Direction Direction = direction;
}
