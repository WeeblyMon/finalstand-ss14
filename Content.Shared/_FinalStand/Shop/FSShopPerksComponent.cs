using Content.Shared._FinalStand.Perks;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Shop;

[RegisterComponent]
public sealed partial class FSShopPerksComponent : Component
{
    [DataField]
    public List<ProtoId<PerkPrototype>> Perks = [];
}
