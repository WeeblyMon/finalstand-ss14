using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Armor.Shop;

[Serializable, NetSerializable]
public enum FSArmorShopUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FSArmorShopBuyMessage(string tierId) : BoundUserInterfaceMessage
{
    public readonly string TierId = tierId;
}

[Serializable, NetSerializable]
public sealed class FSArmorShopState(string? equippedTierId, int credits) : BoundUserInterfaceMessage
{
    public readonly string? EquippedTierId = equippedTierId;
    public readonly int Credits = credits;
}
