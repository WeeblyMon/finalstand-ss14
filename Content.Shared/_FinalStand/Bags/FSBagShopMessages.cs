using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Bags;

[Serializable, NetSerializable]
public enum FSBagShopUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FSBagShopBuyMessage(EntProtoId proto) : BoundUserInterfaceMessage
{
    public readonly EntProtoId Proto = proto;
}

// Text goes as keys so each client shows its own language.
[Serializable, NetSerializable]
public sealed record FSBagShopEntryState(EntProtoId Proto, LocId Name, LocId? Description, int Price);

[Serializable, NetSerializable]
public sealed class FSBagShopState(List<FSBagShopEntryState> entries, int credits) : BoundUserInterfaceMessage
{
    public readonly List<FSBagShopEntryState> Entries = entries;
    public readonly int Credits = credits;
}
