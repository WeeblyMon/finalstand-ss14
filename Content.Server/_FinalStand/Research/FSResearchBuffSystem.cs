using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Research;

// Mirrors FSPerkBuffSystem, but reads FSResearchSystem.IsNodeUnlocked (station-wide) instead of per-mind perk levels.
public sealed class FSResearchBuffSystem : EntitySystem
{
    [Dependency] private readonly FSResearchSystem _research = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;

    private static readonly ProtoId<TagPrototype> BallisticTag = "WeaponGunBallistic";
    private static readonly ProtoId<TagPrototype> EnergyTag = "WeaponGunEnergy";
    private static readonly ProtoId<TagPrototype> LauncherTag = "WeaponGunLauncher";

    private const string L6Proto = "FSWeaponLightMachineGunL6";
    private const string HydraProto = "WeaponLauncherHydraFS";
    private const string RpgProto = "FSWeaponLauncherRocket";
    private const string XrayProto = "WeaponXrayCannonFS";
    private const string TeslaProto = "WeaponTeslaGunFS";
    private const string HarvesterProto = "WeaponHarvesterFS";

    public override void Initialize()
    {
        base.Initialize();
        // Subscribed on TagComponent since FSPerkBuffSystem already owns GunComponent+GunRefreshModifiersEvent.
        SubscribeLocalEvent<TagComponent, GunRefreshModifiersEvent>(OnRefreshModifiers);
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnProjectileHit);
        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnNodeCompleted);
    }

    // Force every live Ordnance gun to re-evaluate the moment a node completes.
    private void OnNodeCompleted(FSResearchNodeCompletedEvent ev)
    {
        var query = EntityQueryEnumerator<GunComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (IsOrdnanceWeapon(uid))
                _gun.RefreshModifiers(uid);
        }
    }

    private bool IsOrdnanceWeapon(EntityUid uid)
    {
        return _tags.HasTag(uid, BallisticTag) || _tags.HasTag(uid, EnergyTag) || _tags.HasTag(uid, LauncherTag);
    }

    private bool Unlocked(string nodeId) => _research.IsNodeUnlocked(nodeId);

    private static Angle ScaleAngle(Angle a, double factor) => new(a.Theta * factor);

    // Pure - shared with FSPlayerBonusSummarySystem.
    public (float FireRateMul, float ReloadPct) GetFireRateReloadTotals(
        bool isBallistic, bool isL6, bool isMinigun, bool isHydra, bool isEnergy, bool isTesla, bool isHarvester = false)
    {
        var fireRateMul = 1f;
        var reloadPct = 0f;

        if (isHarvester)
        {
            if (Unlocked("FSOrdnanceHarvesterCoolantLoop")) fireRateMul *= 1.05f;
            if (Unlocked("FSOrdnanceHarvesterOscillatorTuning")) fireRateMul *= 1.05f;
            if (Unlocked("FSOrdnanceHarvesterOverdrive")) fireRateMul *= 1.08f;
        }

        if (isBallistic)
        {
            if (Unlocked("FSOrdnanceMinigunHighTorqueGearboxes")) fireRateMul *= 1.025f;
            if (Unlocked("FSOrdnanceMinigunDisintegratingLinks")) reloadPct += 0.025f;
            if (Unlocked("FSOrdnanceMinigun12vElectricActuators")) fireRateMul *= 1.03f;
            if (Unlocked("FSOrdnanceMinigunArmouredSolenoids")) reloadPct += 0.03f;
            if (Unlocked("FSOrdnanceMinigunRotaryDriveShafts")) fireRateMul *= 1.03f;

            if (Unlocked("FSOrdnanceL6GasPistonHarnessing")) fireRateMul *= 1.015f;
            if (Unlocked("FSOrdnanceL6FeedSystem")) reloadPct += 0.02f;
        }

        if (isL6)
        {
            if (Unlocked("FSOrdnanceL6GasPistonHarnessing")) fireRateMul *= 1.015f;
            if (Unlocked("FSOrdnanceL6FeedSystem")) reloadPct += 0.02f;

            if (Unlocked("FSOrdnanceL6SoftPackAmmoBags")) reloadPct += 0.12f;
            if (Unlocked("FSOrdnanceL6DisintegratingBeltLinks")) reloadPct -= 0.10f;
            if (Unlocked("FSOrdnanceL6OpenBoltFiring")) fireRateMul *= 1.10f;
            if (Unlocked("FSOrdnanceL6ConstantRecoilSystem")) fireRateMul *= 0.92f;

            if (Unlocked("FSOrdnanceL6ModernAutomaticSaw")) fireRateMul *= 1.05f;
        }

        if (isMinigun)
        {
            if (Unlocked("FSOrdnanceMinigunHighTorqueGearboxes")) fireRateMul *= 1.02f;
            if (Unlocked("FSOrdnanceMinigunDisintegratingLinks")) reloadPct += 0.02f;
            if (Unlocked("FSOrdnanceMinigun12vElectricActuators")) fireRateMul *= 1.025f;
            if (Unlocked("FSOrdnanceMinigunArmouredSolenoids")) reloadPct += 0.025f;
            if (Unlocked("FSOrdnanceMinigunRotaryDriveShafts")) fireRateMul *= 1.025f;

            if (Unlocked("FSOrdnanceMinigunExternalPowerDriveHookups")) fireRateMul *= 1.12f;
            if (Unlocked("FSOrdnanceMinigunInternalBatteryPacks")) fireRateMul *= 0.92f;

            if (Unlocked("FSOrdnanceMinigun")) fireRateMul *= 1.05f;
        }

        if (isHydra)
        {
            if (Unlocked("FSOrdnanceHeavyCylinderIndexing")) fireRateMul *= 1.04f;
            if (Unlocked("FSOrdnance40mmRimmedCasings")) reloadPct += 0.06f;
            if (Unlocked("FSOrdnanceHydra")) fireRateMul *= 1.05f;
        }

        if (isEnergy)
        {
            if (Unlocked("FSOrdnancePulseWidthModulators")) fireRateMul *= 1.03f;
        }

        if (isTesla)
        {
            if (Unlocked("FSOrdnancePulseWidthModulators")) fireRateMul *= 1.02f;
        }

        return (fireRateMul, reloadPct);
    }

    // Pure - shared with FSPlayerBonusSummarySystem.
    public float GetDamageMultiplier(
        bool isBallistic, bool isEnergy, bool isLauncher,
        bool isL6, bool isMinigun, bool isHydra, bool isRpg, bool isXray, bool isTesla, bool isHarvester = false)
    {
        var mul = 1f;

        if (isHarvester)
        {
            if (Unlocked("FSOrdnanceHarvesterFocusingLens")) mul *= 1.05f;
            if (Unlocked("FSOrdnanceHarvesterCapacitorBank")) mul *= 1.05f;
            if (Unlocked("FSOrdnanceHarvesterOverdrive")) mul *= 1.08f;
        }

        if (isBallistic)
        {
            if (Unlocked("FSOrdnanceL6HeavyDutyReceivers")) mul *= 1.02f;
            if (Unlocked("FSOrdnanceMinigunAxialFeedChutes")) mul *= 1.02f;
        }
        if (isMinigun)
        {
            if (Unlocked("FSOrdnanceMinigunAxialFeedChutes")) mul *= 1.02f;
            if (Unlocked("FSOrdnanceMinigun")) mul *= 1.05f;
        }
        if (isL6)
        {
            if (Unlocked("FSOrdnanceL6ModernAutomaticSaw")) mul *= 1.05f;
        }

        if (isLauncher)
        {
            if (Unlocked("FSOrdnanceExplosiveTechnology")) mul *= 1.02f;
            if (Unlocked("FSOrdnancePercussionCapFusing")) mul *= 1.02f;
        }
        if (isHydra)
        {
            if (Unlocked("FSOrdnanceInternalGasPistons")) mul *= 1.03f;
            if (Unlocked("FSOrdnanceHydra")) mul *= 1.05f;
        }
        if (isRpg)
        {
            if (Unlocked("FSOrdnanceThermobaricPayloads")) mul *= 0.90f;
            if (Unlocked("FSOrdnanceRpg7")) mul *= 1.05f;
        }

        if (isEnergy)
        {
            if (Unlocked("FSOrdnanceWeaponizedLaserManipulation")) mul *= 1.02f;
            if (Unlocked("FSOrdnanceConcentratedLaserWeaponry")) mul *= 1.03f;
            if (Unlocked("FSOrdnanceMagnetoHydrodynamicCores")) mul *= 1.03f;
            if (Unlocked("FSOrdnanceArcStabilizingGroundRods")) mul *= 1.03f;
            if (Unlocked("FSOrdnanceCascadeDischargeRelays")) mul *= 1.035f;
        }
        if (isXray)
        {
            if (Unlocked("FSOrdnanceConcentratedLaserWeaponry")) mul *= 1.02f;
            if (Unlocked("FSOrdnanceFocusingCrystalPrisms")) mul *= 1.10f;
            if (Unlocked("FSOrdnanceCollimatorFlanges")) mul *= 0.95f;
            if (Unlocked("FSOrdnanceXrayCannon")) mul *= 1.05f;
        }
        if (isTesla)
        {
            if (Unlocked("FSOrdnanceMagnetoHydrodynamicCores")) mul *= 1.02f;
            if (Unlocked("FSOrdnanceArcStabilizingGroundRods")) mul *= 1.02f;
            if (Unlocked("FSOrdnanceCascadeDischargeRelays")) mul *= 1.02f;
            if (Unlocked("FSOrdnanceChainLightningBridging")) mul *= 0.92f;
            if (Unlocked("FSOrdnanceTeslaCannon")) mul *= 1.05f;
        }

        return mul;
    }

    private void OnRefreshModifiers(EntityUid uid, TagComponent tagComp, ref GunRefreshModifiersEvent args)
    {
        var isBallistic = _tags.HasTag(uid, BallisticTag);
        var isEnergy = _tags.HasTag(uid, EnergyTag);
        var isLauncher = _tags.HasTag(uid, LauncherTag);
        var protoId = Prototype(uid)?.ID;
        var isHarvester = protoId == HarvesterProto;
        if (!isBallistic && !isEnergy && !isLauncher && !isHarvester)
            return;

        var isL6 = protoId == L6Proto;
        var isMinigun = HasComp<FSMinigunComponent>(uid);
        var isHydra = protoId == HydraProto;
        var isRpg = protoId == RpgProto;
        var isXray = protoId == XrayProto;
        var isTesla = protoId == TeslaProto;

        var accuracyPct = 0.0; // blended into MinAngle/MaxAngle/AngleIncrease
        var angleIncreasePct = 0.0;
        var angleDecayPct = 0.0;
        var projectileSpeedMul = 1f;

        var (fireRateMul, reloadPct) = GetFireRateReloadTotals(isBallistic, isL6, isMinigun, isHydra, isEnergy, isTesla, isHarvester);

        if (isBallistic)
        {
            if (Unlocked("FSOrdnanceMinigunClusterBarrelCollars")) accuracyPct += 0.025;
            if (Unlocked("FSOrdnanceMinigunChromedBoreLinings")) projectileSpeedMul *= 1.02f;
            if (Unlocked("FSOrdnanceMinigunHeatSinkJackets")) projectileSpeedMul *= 1.025f;
            if (Unlocked("FSOrdnanceMinigunOpticalSightMounts")) accuracyPct += 0.035;
            if (Unlocked("FSOrdnanceMinigunCounterWeightBuffers")) { angleIncreasePct += 0.035; angleDecayPct += 0.035; }

            if (Unlocked("FSOrdnanceL6BarrelInterchange")) accuracyPct += 0.015;
            if (Unlocked("FSOrdnanceL6ReceiverDesign")) { angleIncreasePct += 0.025; angleDecayPct += 0.025; }
        }

        if (isL6)
        {
            if (Unlocked("FSOrdnanceL6BarrelInterchange")) accuracyPct += 0.015;
            if (Unlocked("FSOrdnanceL6ReceiverDesign")) { angleIncreasePct += 0.025; angleDecayPct += 0.025; }

            if (Unlocked("FSOrdnanceL6QuickChangeBarrels"))
            {
                EnsureComp<FSOverclockedComponent>(uid).ResearchRampMultiplier = 1.15f;
                projectileSpeedMul *= 0.94f;
            }
            if (Unlocked("FSOrdnanceL6FlutedBarrels"))
            {
                accuracyPct += 0.06;
                projectileSpeedMul *= 1.08f;
                EnsureComp<FSOverclockedComponent>(uid).ResearchRampMultiplier = 0.85f;
            }
            if (Unlocked("FSOrdnanceL6OpenBoltFiring"))
            {
                args.MinAngle += Angle.FromDegrees(8);
            }
            if (Unlocked("FSOrdnanceL6ConstantRecoilSystem"))
            {
                angleIncreasePct += 0.10;
                var reduced = args.MinAngle - Angle.FromDegrees(4);
                args.MinAngle = reduced.Theta > 0 ? reduced : new Angle(0);
            }
        }

        if (isMinigun)
        {
            if (Unlocked("FSOrdnanceMinigunClusterBarrelCollars")) accuracyPct += 0.02;
            if (Unlocked("FSOrdnanceMinigunChromedBoreLinings")) projectileSpeedMul *= 1.015f;
            if (Unlocked("FSOrdnanceMinigunHeatSinkJackets")) projectileSpeedMul *= 1.02f;
            if (Unlocked("FSOrdnanceMinigunOpticalSightMounts")) accuracyPct += 0.03;
            if (Unlocked("FSOrdnanceMinigunCounterWeightBuffers")) { angleIncreasePct += 0.03; angleDecayPct += 0.03; }

            if (Unlocked("FSOrdnanceMinigun")) accuracyPct += 0.05;
        }

        if (isLauncher)
        {
            if (Unlocked("FSOrdnanceHighLowGasChambers")) accuracyPct += 0.025;
        }

        if (isHydra)
        {
            if (Unlocked("FSOrdnanceSpigotSleeves"))
            {
                projectileSpeedMul *= 1.10f;
                args.MaxAngle += Angle.FromDegrees(6);
                args.AngleIncrease += Angle.FromDegrees(1.5);
            }
            if (Unlocked("FSOrdnanceVentedChambers")) projectileSpeedMul *= 0.90f;
        }

        if (isRpg)
        {
            if (Unlocked("FSOrdnanceCounterMassVentingTubes")) accuracyPct += 0.05;
            if (Unlocked("FSOrdnanceVenturiExhaustNozzles")) projectileSpeedMul *= 1.08f;
            if (Unlocked("FSOrdnanceStabilizingFinAssemblies")) accuracyPct += 0.05;

            if (Unlocked("FSOrdnanceRpg7")) projectileSpeedMul *= 1.05f;
        }

        if (isEnergy)
        {
            if (Unlocked("FSOrdnanceToroidalInductionFields")) accuracyPct += 0.03;
            if (Unlocked("FSOrdnanceHighFrequencyTeslaCoils")) projectileSpeedMul *= 1.04f;
        }

        if (isXray)
        {
            if (Unlocked("FSOrdnanceFocusingCrystalPrisms")) { /* damage handled in Hook B */ }
        }

        if (isTesla)
        {
            if (Unlocked("FSOrdnanceToroidalInductionFields")) accuracyPct += 0.02;
            if (Unlocked("FSOrdnanceHighFrequencyTeslaCoils")) projectileSpeedMul *= 1.02f;

            if (Unlocked("FSOrdnanceOverchargeCapacitors")) projectileSpeedMul *= 0.95f;
        }

        args.FireRate *= fireRateMul;
        args.ProjectileSpeed *= projectileSpeedMul;
        if (accuracyPct > 0)
        {
            args.MinAngle = ScaleAngle(args.MinAngle, 1 - accuracyPct);
            args.MaxAngle = ScaleAngle(args.MaxAngle, 1 - accuracyPct * 0.6);
            args.AngleIncrease = ScaleAngle(args.AngleIncrease, 1 - accuracyPct * 0.4);
        }
        if (angleIncreasePct != 0)
            args.AngleIncrease = ScaleAngle(args.AngleIncrease, Math.Max(0.1, 1 - angleIncreasePct));
        if (angleDecayPct != 0)
            args.AngleDecay = ScaleAngle(args.AngleDecay, 1 + angleDecayPct);

        var state = EnsureComp<FSWeaponUpgradeStateComponent>(uid);
        var newReloadMul = Math.Clamp(1f - reloadPct, 0.3f, 2f);
        if (!MathHelper.CloseToPercent(state.ResearchReloadMultiplier, newReloadMul))
        {
            state.ResearchReloadMultiplier = newReloadMul;
            Dirty(uid, state);
        }
    }

    private void OnProjectileHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon is not { } weapon)
            return;

        var isBallistic = _tags.HasTag(weapon, BallisticTag);
        var isEnergy = _tags.HasTag(weapon, EnergyTag);
        var isLauncher = _tags.HasTag(weapon, LauncherTag);
        if (!isBallistic && !isEnergy && !isLauncher)
            return;

        var protoId = Prototype(weapon)?.ID;
        var isL6 = protoId == L6Proto;
        var isMinigun = HasComp<FSMinigunComponent>(weapon);
        var isHydra = protoId == HydraProto;
        var isRpg = protoId == RpgProto;
        var isXray = protoId == XrayProto;
        var isTesla = protoId == TeslaProto;

        var mul = GetDamageMultiplier(isBallistic, isEnergy, isLauncher, isL6, isMinigun, isHydra, isRpg, isXray, isTesla);

        if (mul != 1f)
            ev.AdditionalMultiplier *= mul;
    }
}
