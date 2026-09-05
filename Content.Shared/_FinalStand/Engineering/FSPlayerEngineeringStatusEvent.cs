using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Engineering;

// Lets FSShopClientSystem show engineering-locked shops without replicating Mind/Job data.
[Serializable, NetSerializable]
public sealed class FSPlayerEngineeringStatusEvent(bool isEngineering) : EntityEventArgs
{
    public readonly bool IsEngineering = isEngineering;
}
