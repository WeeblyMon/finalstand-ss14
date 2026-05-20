using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Economy;

[Serializable, NetSerializable]
public sealed class WalletUpdatedEvent : EntityEventArgs
{
    public readonly int Credits;
    public readonly int AugmentPoints;

    public WalletUpdatedEvent(int credits, int augmentPoints)
    {
        Credits = credits;
        AugmentPoints = augmentPoints;
    }
}
