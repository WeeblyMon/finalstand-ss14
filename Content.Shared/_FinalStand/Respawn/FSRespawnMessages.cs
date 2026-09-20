using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Respawn;

[Serializable, NetSerializable]
public sealed class FSRespawnRequestMessage : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class FSRespawnOfferRequestEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class FSRespawnOfferEvent : EntityEventArgs
{
    public bool Available;
    public int Cost;

    public FSRespawnOfferEvent(bool available, int cost)
    {
        Available = available;
        Cost = cost;
    }
}
