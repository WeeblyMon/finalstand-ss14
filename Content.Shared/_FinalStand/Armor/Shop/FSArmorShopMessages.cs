using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Armor.Shop;

[Serializable, NetSerializable]
public enum FSArmorShopUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FSArmorShopBuyMessage(ProtoId<FSArmorTierPrototype> tier) : BoundUserInterfaceMessage
{
    public readonly ProtoId<FSArmorTierPrototype> Tier = tier;
}

[Serializable, NetSerializable]
public sealed class FSArmorShopState(ProtoId<FSArmorTierPrototype>? equipped, int credits) : BoundUserInterfaceMessage
{
    public readonly ProtoId<FSArmorTierPrototype>? Equipped = equipped;
    public readonly int Credits = credits;
}
