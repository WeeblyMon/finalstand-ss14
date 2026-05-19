using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Perks;

[Serializable, NetSerializable]
public sealed class PerkAddedEvent : EntityEventArgs
{
    public NetEntity Player { get; init; }
    public PerkType Perk { get; init; }
}

[Serializable, NetSerializable]
public sealed class PerkRemovedAllEvent : EntityEventArgs
{
    public NetEntity Player { get; init; }
}
