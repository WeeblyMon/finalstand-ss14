using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Shop;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSWeaponUpgradeStateComponent : Component
{
    [DataField] public float CritChance = 0f;
    [DataField] public float CritDamageMultiplier = 2f;
    [DataField] public FixedPoint2 PierceThreshold = FixedPoint2.Zero;

    [DataField] public Dictionary<string, int> Levels = new();

    [DataField] public int ExplosiveShotLevel = 0;
    [DataField] public int MoneyGainBonusPerKill = 0;
    [DataField] public int MoneyPerHitBonus = 0;
    [DataField] public bool SlowingEnabled = false;
    [DataField] public int BeamChainTargets = 0;
    [DataField] public int KnockbackLevel = 0;
    [DataField] public bool SetOnFireEnabled = false;
    [DataField] public bool APRoundsEnabled = false;
    [DataField] public float ArmorShredMagnitude = 0f;
    [DataField] public float ReloadSpeedMultiplier = 1.0f;
    [DataField] public bool SpeedLoaderEnabled = false;
    [DataField] public float LifeStealPercent = 0f;
    [DataField] public int StaminaStealLevel = 0;
    [DataField] public float DamageMultiplier = 1.0f;
    [DataField] public int ExtraPellets = 0;
    [DataField] public bool ScrapshotEnabled = false;
    [DataField] public int BleedLevel = 0;
    [DataField] public bool FlechetteEnabled = false;
    [DataField] public bool SplinterImpactEnabled = false;
    [DataField] public bool OverchargeShotEnabled = false;
    [DataField] public float PelletSpreadMultiplier = 1.0f;

    [DataField] public int OverkillLevel = 0;
    [DataField, AutoNetworkedField] public bool ExecutionEnabled = false;
    [DataField] public bool WarTornEnabled = false;
    [DataField] public int SuppressionLevel = 0;
    [DataField] public bool ResonanceEnabled = false;
    [DataField] public int PrismaticLevel = 0;
    [DataField] public int MagEfficiencyLevel = 0;
    [DataField] public bool PulseCascadeEnabled = false;
    [DataField] public bool AftershockEnabled = false;

    [DataField] public int MagazineSizeBonus = 0;
    [DataField] public float BatteryFireCostReduction = 0f;

    [DataField] public float AttackSpeedMultiplier = 1f;
    [DataField] public int ConcussionClubStunMs = 0;
    [DataField] public bool CritVsStunned = false;
    [DataField] public int StunOnHitMs = 0;
    [DataField] public int FlintlockCritDurationSec = 0;
    [DataField] public bool CritVsBurning = false;
    [DataField] public float FireDamageResist = 0f;
    [DataField] public bool WhileBurningBuff = false;
    [DataField] public float FuelEfficiencyReduction = 0f;
    [DataField] public float FuelCapacityMultiplier = 1f;
    [DataField] public float WielderResistance = 0f;
    // one-shot flag: EnergySword transform only runs once per entity
    [DataField] public bool DualWieldEnergySwordApplied = false;
    [DataField] public float HeldSpeedBonusPercent = 0f;

    [DataField] public int TotalSpent = 0;

    [DataField, AutoNetworkedField] public bool KnifeGolden = false;

    [DataField] public bool VaporiseWeakMobEnabled = false;
    [DataField] public bool PointBlankCritEnabled = false;
    [DataField] public bool ExecutionShotUpgradeEnabled = false;

    [DataField] public bool ClusterBarrageEnabled = false;
    [DataField] public int BlastRadiusBonus = 0;
    [DataField] public int ShapedChargeLevel = 0;
    [DataField] public int RadiationCoatingLevel = 0;
    [DataField] public float TeslaArcRangeBonus = 0f;

    // Ordnance research: recomputed from scratch every GunRefreshModifiersEvent, not accumulated.
    [DataField] public float ResearchReloadMultiplier = 1f;
    [DataField] public int TeslaChainTargetBonus = 0;
    [DataField] public float KnockbackResearchForceBonus = 0f;
    [DataField] public float RadiationCoatingResearchBonus = 0f;
}
