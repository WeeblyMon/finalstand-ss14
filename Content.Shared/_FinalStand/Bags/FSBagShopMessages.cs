using Robust.Shared.Localization;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Bags;

[Serializable, NetSerializable]
public enum FSBagShopUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FSBagShopBuyMessage(string protoId) : BoundUserInterfaceMessage
{
    public readonly string ProtoId = protoId;
}

// Text goes as keys so each client shows its own language.
[Serializable, NetSerializable]
public sealed record FSBagShopEntryState(string ProtoId, LocId Name, LocId? Description, int Price);

[Serializable, NetSerializable]
public sealed class FSBagShopState(List<FSBagShopEntryState> entries, int credits) : BoundUserInterfaceMessage
{
    public readonly List<FSBagShopEntryState> Entries = entries;
    public readonly int Credits = credits;
}
