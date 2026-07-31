using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Science;

// Pushed to a player's own client on spawn so FSShopClientSystem can show science-locked shops without replicating Mind/Job data.
[Serializable, NetSerializable]
public sealed class FSPlayerScienceStatusEvent(bool isScience) : EntityEventArgs
{
    public readonly bool IsScience = isScience;
}
