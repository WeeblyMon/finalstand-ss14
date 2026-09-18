using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Shop;

[RegisterComponent]
public sealed partial class FSShopWeaponComponent : Component
{
    [DataField]
    public EntProtoId? WeaponProtoId;

    [DataField]
    public ProtoId<FSTechNodePrototype>? RequiresResearch;

    [DataField]
    public bool RequiresScience;

    [DataField]
    public bool RequiresEngineering;

    [DataField]
    public bool SinglePurchase;

    [DataField]
    public List<EntProtoId> WeaponProtoIdAliases = [];

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

    [DataField] public int StatDamage = 50;
    [DataField] public int StatFireRate = 50;
    [DataField] public int StatAccuracy = 50;
    [DataField] public int StatCapacity = 50;

    [DataField] public int StatHealth;
}
