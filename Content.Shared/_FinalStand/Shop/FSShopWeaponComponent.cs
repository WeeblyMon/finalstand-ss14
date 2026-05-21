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

    [DataField]
    public string Category = "";

    // 0–100 designer-set values for the shop UI stat bars.
    [DataField] public byte StatDamage   = 50;
    [DataField] public byte StatFireRate = 50;
    [DataField] public byte StatAccuracy = 50;
    [DataField] public byte StatCapacity = 50;
}
