using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Respawn;

// Carries no cost — the server quotes and charges its own figure, never the client's.
[Serializable, NetSerializable]
public sealed class FSRespawnRequestMessage : EntityEventArgs
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
