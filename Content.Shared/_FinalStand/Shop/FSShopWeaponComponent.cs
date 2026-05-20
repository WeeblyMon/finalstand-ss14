using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Shop;

[RegisterComponent]
public sealed partial class FSShopWeaponComponent : Component
{
    [DataField]
    public EntProtoId? WeaponProtoId;

    [DataField]
    public int Price = 500;

    [DataField]
    public List<WeaponUpgradeDef> Upgrades = [];

    [DataField]
    public EntProtoId? StarterAmmoProtoId;

    [DataField]
    public int StarterAmmoCount = 1;
}
