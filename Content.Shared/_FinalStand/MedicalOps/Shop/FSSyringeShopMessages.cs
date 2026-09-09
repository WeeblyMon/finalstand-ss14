using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps.Shop;

[Serializable, NetSerializable]
public sealed class FSSyringeShopBuyMessage(string tierId) : BoundUserInterfaceMessage
{
    public readonly string TierId = tierId;
}
