using Content.Shared.FixedPoint;

namespace Content.Shared._FinalStand.Shop;

[RegisterComponent]
public sealed partial class FSWeaponUpgradeStateComponent : Component
{
    [DataField] public float CritChance = 0f;
    [DataField] public float CritDamageMultiplier = 2f;
    [DataField] public FixedPoint2 PierceThreshold = FixedPoint2.Zero;

    [DataField] public Dictionary<string, int> Levels = new();

    [DataField] public int ExplosiveShotLevel = 0;
    [DataField] public int MoneyGainBonusPerKill = 0;
    [DataField] public bool SlowingEnabled = false;
    [DataField] public int BeamChainTargets = 0;
    [DataField] public int KnockbackLevel = 0;
    [DataField] public bool SetOnFireEnabled = false;
    [DataField] public bool APRoundsEnabled = false;
    [DataField] public float ArmorShredMagnitude = 0f;
    [DataField] public float ReloadSpeedMultiplier = 1.0f;
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
    [DataField] public bool ExecutionEnabled = false;
    [DataField] public bool WarTornEnabled = false;
    [DataField] public int SuppressionLevel = 0;
    [DataField] public bool ResonanceEnabled = false;
    [DataField] public int PrismaticLevel = 0;
    [DataField] public int MagEfficiencyLevel = 0;
    [DataField] public bool PulseCascadeEnabled = false;
    [DataField] public bool AftershockEnabled = false;

    // Accumulated magazine size bonus for ChamberMagazineAmmoProvider guns (e.g. MK58).
    // Re-applied each time a new magazine is inserted.
    [DataField] public int MagazineSizeBonus = 0;

    // TODO(finalstand): audit existing purchased weapons for TotalSpent backfill if needed
    [DataField] public int TotalSpent = 0;
}
