using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Shop;

// Client -> server: player confirmed weapon sell (second button press).
[Serializable, NetSerializable]
public sealed class FSShopSellMessage : BoundUserInterfaceMessage { }

// Server -> client: weapon sold successfully; client resets confirmation state and refreshes UI.
[Serializable, NetSerializable]
public sealed class FSShopSellCompletedEvent : EntityEventArgs { }

// Server -> client: sell rejected, with a reason string for display/debugging.
[Serializable, NetSerializable]
public sealed class FSShopSellFailedEvent : EntityEventArgs
{
    public readonly string Reason;
    public FSShopSellFailedEvent(string reason) { Reason = reason; }
}
