using Content.Shared._FinalStand.Perks;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Shop;

[Serializable, NetSerializable]
public sealed class FSShopBuyPerkMessage(string perkProtoId) : BoundUserInterfaceMessage
{
    public readonly string PerkProtoId = perkProtoId;
}
