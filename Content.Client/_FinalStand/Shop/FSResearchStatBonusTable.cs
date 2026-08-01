namespace Content.Client._FinalStand.Shop;

// Which Ordnance research nodes touch which shop stat bar, per weapon - display only, not math.
public static class FSResearchStatBonusTable
{
    public const string Damage = "Damage";
    public const string FireRate = "FireRate";
    public const string Accuracy = "Accuracy";
    public const string Capacity = "Capacity";

    private static readonly Dictionary<string, Dictionary<string, string[]>> Table = new()
    {
        ["FSWeaponLightMachineGunL6"] = new()
        {
            [Damage] = ["FSOrdnanceL6HeavyDutyReceivers", "FSOrdnanceL6ModernAutomaticSaw"],
            [FireRate] =
            [
                "FSOrdnanceL6GasPistonHarnessing", "FSOrdnanceMinigunHighTorqueGearboxes",
                "FSOrdnanceMinigun12vElectricActuators", "FSOrdnanceMinigunRotaryDriveShafts",
                "FSOrdnanceL6OpenBoltFiring", "FSOrdnanceL6ConstantRecoilSystem", "FSOrdnanceL6ModernAutomaticSaw",
            ],
            [Accuracy] =
            [
                "FSOrdnanceL6BarrelInterchange", "FSOrdnanceMinigunClusterBarrelCollars",
                "FSOrdnanceMinigunOpticalSightMounts", "FSOrdnanceL6FlutedBarrels",
            ],
            [Capacity] = ["FSOrdnanceL6BasicMunitions", "FSOrdnanceL6SoftPackAmmoBags", "FSOrdnanceL6DisintegratingBeltLinks"],
        },
        ["FSWeaponMinigun"] = new()
        {
            [Damage] = ["FSOrdnanceL6HeavyDutyReceivers", "FSOrdnanceMinigunAxialFeedChutes", "FSOrdnanceMinigunCoreCastingVats", "FSOrdnanceMinigun"],
            [FireRate] =
            [
                "FSOrdnanceMinigunHighTorqueGearboxes", "FSOrdnanceMinigun12vElectricActuators",
                "FSOrdnanceMinigunRotaryDriveShafts", "FSOrdnanceMinigunExternalPowerDriveHookups",
                "FSOrdnanceMinigunInternalBatteryPacks", "FSOrdnanceMinigunBarrelOverclockSync", "FSOrdnanceMinigun",
            ],
            [Accuracy] = ["FSOrdnanceMinigunClusterBarrelCollars", "FSOrdnanceMinigunOpticalSightMounts", "FSOrdnanceMinigunGyroMatrixAugment", "FSOrdnanceMinigun"],
            [Capacity] = ["FSOrdnanceL6BasicMunitions", "FSOrdnanceMinigunSynchronizedFeedGates", "FSOrdnanceMinigunExtendedDrumTuning"],
        },
        ["WeaponLauncherHydraFS"] = new()
        {
            [Damage] = ["FSOrdnanceExplosiveTechnology", "FSOrdnancePercussionCapFusing", "FSOrdnanceInternalGasPistons", "FSOrdnanceHydra"],
            [FireRate] = ["FSOrdnanceHeavyCylinderIndexing", "FSOrdnanceHydra"],
            [Accuracy] = ["FSOrdnanceHighLowGasChambers", "FSOrdnanceSpigotSleeves"],
            [Capacity] = ["FSOrdnanceVentedChambers", "FSOrdnanceHydra"],
        },
        ["FSWeaponLauncherRocket"] = new()
        {
            [Damage] = ["FSOrdnanceExplosiveTechnology", "FSOrdnancePercussionCapFusing", "FSOrdnanceThermobaricPayloads", "FSOrdnanceRpg7"],
            [FireRate] = [],
            [Accuracy] = ["FSOrdnanceHighLowGasChambers", "FSOrdnanceCounterMassVentingTubes", "FSOrdnanceStabilizingFinAssemblies"],
            [Capacity] = [],
        },
        ["WeaponXrayCannonFS"] = new()
        {
            [Damage] = ["FSOrdnanceWeaponizedLaserManipulation", "FSOrdnanceConcentratedLaserWeaponry", "FSOrdnanceFocusingCrystalPrisms", "FSOrdnanceCollimatorFlanges", "FSOrdnanceFocalOverdrive", "FSOrdnanceXrayCannon"],
            [FireRate] = ["FSOrdnanceFrequencySync"],
            [Accuracy] = ["FSOrdnanceCollimatorFlanges"],
            [Capacity] = ["FSOrdnanceHighOutputCapacitors", "FSOrdnanceCryogenicCoolingPumps", "FSOrdnanceFocusingCrystalPrisms", "FSOrdnanceXrayCannon"],
        },
        ["WeaponTeslaGunFS"] = new()
        {
            [Damage] = ["FSOrdnanceWeaponizedLaserManipulation", "FSOrdnanceMagnetoHydrodynamicCores", "FSOrdnanceArcStabilizingGroundRods", "FSOrdnanceCascadeDischargeRelays", "FSOrdnanceChainLightningBridging", "FSOrdnanceTeslaCannon"],
            [FireRate] = ["FSOrdnancePulseWidthModulators", "FSOrdnanceOverclockedTiming", "FSOrdnanceTeslaCannon"],
            [Accuracy] = ["FSOrdnanceToroidalInductionFields"],
            [Capacity] = ["FSOrdnanceHighOutputCapacitors", "FSOrdnanceSubAtomicParticleIonizers", "FSOrdnanceHeavyStepUpTransformers", "FSOrdnanceOverchargeCapacitors", "FSOrdnanceFluxTuning", "FSOrdnanceDenseLithiumStorage", "FSOrdnanceTeslaCannon"],
        },
    };

    public static string[] GetNodes(string? weaponProtoId, string category)
    {
        if (weaponProtoId == null || !Table.TryGetValue(weaponProtoId, out var byCategory))
            return [];
        return byCategory.GetValueOrDefault(category, []);
    }
}
