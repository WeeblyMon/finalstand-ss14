using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Shop;

public enum WeaponUpgradeType : byte
{
    FireRate,
    AngleMax,
    SpawnItem,
    Accuracy,
    MagazineSize,
    ReloadSpeed,
    Range,
    FullAuto,
    CritChance,
    CritDamage,
    Pierce,
    Radius,
    ExplosiveShot,
    MoneyGainBonus,
    MoneyPerHit,
    Slowing,
    BeamChaining,
    Knockback,
    SelfChargeSpeed,
    SetOnFire,
    APRounds,
    ArmorShred,
    StaminaSteal,
    LifeSteal,
    MovementSpeed,
    AttackSpeed,
    PelletCount,
    Scrapshot,
    Bleed,
    SlamFire,
    FlechetteRounds,
    SplinterImpact,
    OverchargeShot,
    Damage,
    Overkill,
    Execution,
    WarTorn,
    Suppression,
    Resonance,
    Prismatic,
    MagEfficiency,
    PulseCascade,
    Aftershock,
    SpeedLoader,
    Overclocked,
    IronBeast,

    ConcussionClub,
    CritVsStunned,
    StunOnHit,
    FlintlockCritSynergy,
    CritVsBurning,
    FireResist,
    WhileBurningBuff,
    FuelEfficiency,
    FuelCapacity,
    WielderResistance,
    DualWieldEnergySword,

    GrenadeCapacity,
    GrenadeRegen,
    DeployableCapacity,
    GrenadeBurnDuration,
    GrenadeStunDuration,
    GrenadeBaitDuration,
    GrenadeImpactFuse,
    GrenadeEffectRadius,
    GrenadeBlastBonus,
    GrenadeCluster,

    VaporiseWeakMob,
    PointBlankCrit,
    ExecutionShot,
    MarksmansRhythm,

    ClusterBarrage,
    Barrage,
    ShapedCharge,
    RadiationCoating,
    GravitonCore,
    TeslaArcRange,

    Thorns,
    ShieldVampire,
    ShieldDurability,

    OverloadRound,
    HomingBolts,
    Multishot,
}

[DataDefinition]
public sealed partial class WeaponUpgradeDef
{
    [DataField(required: true)] public string Id = "";
    [DataField] public string Name = "";
    [DataField] public string Description = "";
    [DataField] public int MaxLevel = 5;
    [DataField] public int BaseCost = 100;
    [DataField] public WeaponUpgradeType Type = WeaponUpgradeType.FireRate;
    [DataField] public float ValuePerLevel = 1.0f;
    [DataField] public EntProtoId? SpawnProtoId;
    [DataField] public int SpawnCountPerLevel = 1;
    [DataField] public bool IsStub = false;

    [DataField] public string? RequiresUpgrade;

    [DataField] public EntProtoId? TargetWeaponProtoId;

    // Cost multiplier applied while DiscountResearch is unlocked.
    [DataField] public ProtoId<FSTechNodePrototype>? DiscountResearch;
    [DataField] public float DiscountMultiplier = 1.0f;
}
