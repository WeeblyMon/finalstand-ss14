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
    BattleTrance,
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
    AmmoBoxUses,
    AmmoBoxSpeed,
    SentryAmmo,
    SentryFireRate,
    SentryDamage,
    SentryRange,
    ChargeSpeed,
    BounceCount,
    BounceRetention,
    ChargePierce,
    BounceRefund,
    BounceCrit,
    FractureRounds,
    DeployableRegen,
    LandmineDamage,
    LandmineDetonations,
    LandmineHighExplosive,
    GrenadeBurnDuration,
    GrenadeStunDuration,
    GrenadeBaitDuration,
    GrenadeImpactFuse,
    GrenadeEffectRadius,
    GrenadeBlastBonus,
    GrenadeCluster,
    GrenadeSingularity,

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

    ConvertWeapon,

    // A share of each kill's credits, unlike MoneyGainBonus's flat amount.
    MoneyGainBonusPercent,
}

[DataDefinition]
public sealed partial class WeaponUpgradeDef
{
    [DataField(required: true)] public string Id = "";
    [DataField("name")] public LocId? NameKey;
    [DataField("description")] public LocId? DescriptionKey;
    [DataField] public int MaxLevel = 5;
    [DataField] public int BaseCost = 100;
    [DataField] public WeaponUpgradeType Type = WeaponUpgradeType.FireRate;
    [DataField] public float ValuePerLevel = 1.0f;
    [DataField] public EntProtoId? SpawnProtoId;
    [DataField] public int SpawnCountPerLevel = 1;
    [DataField] public bool IsStub = false;

    [DataField] public string? RequiresUpgrade;

    /// <summary>While this upgrade is owned, the row shows AltName and AltDescription instead.</summary>
    [DataField] public string? AltWhenUpgrade;
    [DataField("altName")] public LocId? AltNameKey;
    [DataField("altDescription")] public LocId? AltDescriptionKey;

    [DataField] public EntProtoId? TargetWeaponProtoId;

    // Cost multiplier applied while DiscountResearch is unlocked.
    [DataField] public ProtoId<FSTechNodePrototype>? RequiresResearch;
    [DataField] public ProtoId<FSTechNodePrototype>? DiscountResearch;
    [DataField] public float DiscountMultiplier = 1.0f;

    public string Name => Text(NameKey);
    public string Description => Text(DescriptionKey);
    public string? AltName => AltNameKey is { } key ? Loc.GetString(key) : null;
    public string? AltDescription => AltDescriptionKey is { } key ? Loc.GetString(key) : null;

    private static string Text(LocId? key) => key is { } id ? Loc.GetString(id) : string.Empty;

    // How much of a shotgun's pellet cone each Accuracy or AngleMax level takes away. On a pellet gun this is most
    // of what the upgrade does, so its description quotes it too.
    public const float PelletSpreadCutPerLevel = 0.16f;

    // Degrees one level takes off the aimed spread (MinAngle), the sustained-fire worst case (MaxAngle) and the
    // growth per shot (AngleIncrease). The upgrade, the shop's stats and the descriptions all go by this.
    public (double Min, double Max, double Increase) AngleDeltaPerLevel() => Type switch
    {
        WeaponUpgradeType.Accuracy => (ValuePerLevel * 0.5, ValuePerLevel * 0.2, ValuePerLevel * 0.3),
        WeaponUpgradeType.AngleMax => (0.0, ValuePerLevel, 0.0),
        _                          => (0.0, 0.0, 0.0),
    };

    private static readonly float[] LevelCostMults = [1.0f, 1.3f, 1.8f, 2.6f, 3.6f];

    // Shared so the shop window shows exactly what the server charges.
    public int LevelCost(int level, bool discounted)
    {
        var mult = level > 0 && level <= LevelCostMults.Length
            ? LevelCostMults[level - 1]
            : 3.6f + (level - LevelCostMults.Length) * 1.2f;
        var cost = (int)MathF.Round(BaseCost * mult);
        return discounted ? (int)MathF.Round(cost * DiscountMultiplier) : cost;
    }
}
