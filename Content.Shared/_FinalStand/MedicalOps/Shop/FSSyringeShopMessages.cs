using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps.Shop;

[Serializable, NetSerializable]
public enum FSSyringeShopUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class FSSyringeShopBuyMessage(string tierId) : BoundUserInterfaceMessage
{
    public readonly string TierId = tierId;
}

[Serializable, NetSerializable]
public sealed class FSSyringeShopState(string? ownedTierId, int credits) : BoundUserInterfaceMessage
{
    public readonly string? OwnedTierId = ownedTierId;
    public readonly int Credits = credits;
}
