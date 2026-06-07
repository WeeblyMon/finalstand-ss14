using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Shop;

/// <summary>Client → server: player confirmed weapon sell (second button press).</summary>
[Serializable, NetSerializable]
public sealed class FSShopSellMessage : BoundUserInterfaceMessage { }

/// <summary>
/// Server → client: weapon sold successfully.
/// Client resets confirmation state and refreshes UI on receive.
/// </summary>
[Serializable, NetSerializable]
public sealed class FSShopSellCompletedEvent : EntityEventArgs { }

/// <summary>
/// Server → client: sell rejected.
/// Contains a reason string for client error display / debugging.
/// </summary>
[Serializable, NetSerializable]
public sealed class FSShopSellFailedEvent : EntityEventArgs
{
    public readonly string Reason;
    public FSShopSellFailedEvent(string reason) { Reason = reason; }
}
