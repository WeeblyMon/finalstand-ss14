using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.FixedPoint;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Research;

// Handles every Ordnance node effect that isn't a GunRefreshModifiersEvent field; applied idempotently via FSResearchAppliedComponent.
public sealed partial class FSResearchStaticGrantSystem : EntitySystem
{
    [Dependency] private FSResearchSystem _research = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private FSWeaponClassifierSystem _classifier = default!;

    public override void Initialize()
    {
        base.Initialize();
        // Subscribed on TagComponent since SharedGunSystem already owns GunComponent+MapInitEvent.
        SubscribeLocalEvent<TagComponent, MapInitEvent>(OnGunMapInit);
        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnNodeCompleted);
    }

    private void OnGunMapInit(EntityUid uid, TagComponent tagComp, MapInitEvent args)
    {
        Reconcile(uid);
    }

    private void OnNodeCompleted(FSResearchNodeCompletedEvent ev)
    {
        var query = EntityQueryEnumerator<GunComponent>();
        while (query.MoveNext(out var uid, out _))
            Reconcile(uid);
    }

    private bool Unlocked(string nodeId) => _research.IsNodeUnlocked(nodeId);

    private static int Delta(FSResearchAppliedComponent tracker, string nodeId, int target)
    {
        var applied = tracker.AppliedLevels.GetValueOrDefault(nodeId, 0);
        if (applied == target)
            return 0;
        tracker.AppliedLevels[nodeId] = target;
        return target - applied;
    }

    public void Reconcile(EntityUid weapon)
    {
        var kind = _classifier.Classify(weapon);
        if (!kind.HasGunTag)
            return;

        var isBallistic = kind.Ballistic;
        var isEnergy = kind.Energy;
        var isL6 = kind.L6;
        var isMinigun = kind.Minigun;
        var isHydra = kind.Hydra;
        var isRpg = kind.Rpg;
        var isXray = kind.Xray;
        var isTesla = kind.Tesla;

        var tracker = EnsureComp<FSResearchAppliedComponent>(weapon);
        var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
        var shopLevels = state.Levels;

        ReconcileCapacity(weapon, tracker, isBallistic, isL6, isMinigun, isHydra);
        ReconcileChargeRate(weapon, tracker, isEnergy, isXray, isTesla);
        ReconcileMisc(weapon, tracker, state, isL6, isMinigun, isXray, isRpg, isTesla);
        ReconcileAugments(weapon, tracker, state, shopLevels, isMinigun, isXray, isTesla);

        if (HasComp<GunComponent>(weapon))
            _gun.RefreshModifiers(weapon);
    }

    // Pure - shared with FSPlayerBonusSummarySystem, mirrors ReconcileCapacity's node list.
    public int GetMagazineFlatBonus(bool isBallistic, bool isL6, bool isMinigun, bool isHydra)
    {
        var total = 0;
        if (isBallistic && Unlocked("FSOrdnanceL6BasicMunitions")) total += 5;
        if (isBallistic && Unlocked("FSOrdnanceMinigunSynchronizedFeedGates")) total += 15;
        if (isMinigun && Unlocked("FSOrdnanceMinigunSynchronizedFeedGates")) total += 15;
        if (isL6 && Unlocked("FSOrdnanceL6SoftPackAmmoBags")) total -= 7;
        if (isL6 && Unlocked("FSOrdnanceL6DisintegratingBeltLinks")) total += 20;
        if (isHydra && Unlocked("FSOrdnanceVentedChambers")) total += 2;
        if (isHydra && Unlocked("FSOrdnanceHydra")) total += 1;
        return total;
    }

    private void ReconcileCapacity(EntityUid weapon, FSResearchAppliedComponent tracker,
        bool isBallistic, bool isL6, bool isMinigun, bool isHydra)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(weapon, out var bal))
            return;

        var total = 0;
        total += Delta(tracker, "FSOrdnanceL6BasicMunitions", isBallistic && Unlocked("FSOrdnanceL6BasicMunitions") ? 1 : 0) * 5;
        total += Delta(tracker, "FSOrdnanceMinigunSynchronizedFeedGates", isBallistic && Unlocked("FSOrdnanceMinigunSynchronizedFeedGates") ? 1 : 0) * 15;
        total += Delta(tracker, "FSOrdnanceMinigunSynchronizedFeedGates-kicker", isMinigun && Unlocked("FSOrdnanceMinigunSynchronizedFeedGates") ? 1 : 0) * 15;
        total += Delta(tracker, "FSOrdnanceL6SoftPackAmmoBags", isL6 && Unlocked("FSOrdnanceL6SoftPackAmmoBags") ? 1 : 0) * -7;
        total += Delta(tracker, "FSOrdnanceL6DisintegratingBeltLinks", isL6 && Unlocked("FSOrdnanceL6DisintegratingBeltLinks") ? 1 : 0) * 20;
        total += Delta(tracker, "FSOrdnanceVentedChambers", isHydra && Unlocked("FSOrdnanceVentedChambers") ? 1 : 0) * 2;
        total += Delta(tracker, "FSOrdnanceHydra", isHydra && Unlocked("FSOrdnanceHydra") ? 1 : 0) * 1;

        if (total == 0)
            return;

#pragma warning disable RA0002
        bal.Capacity = Math.Max(1, bal.Capacity + total);
        if (total > 0)
            bal.UnspawnedCount = Math.Min(bal.UnspawnedCount + total, bal.Capacity);
#pragma warning restore RA0002
        Dirty(weapon, bal);
    }

    private void ReconcileChargeRate(EntityUid weapon, FSResearchAppliedComponent tracker,
        bool isEnergy, bool isXray, bool isTesla)
    {
        if (!TryComp<BatterySelfRechargerComponent>(weapon, out var charger))
            return;

        var mult = 1f;
        void Apply(string nodeId, bool eligible, float pct, string? trackerKey = null)
        {
            var d = Delta(tracker, trackerKey ?? nodeId, eligible && Unlocked(nodeId) ? 1 : 0);
            if (d == 0) return;
            mult *= MathF.Pow(1f + pct, d);
        }

        Apply("FSOrdnanceHighOutputCapacitors", isEnergy, 0.05f);
        Apply("FSOrdnanceCryogenicCoolingPumps", isEnergy, 0.04f);
        Apply("FSOrdnanceCryogenicCoolingPumps", isXray, 0.02f, "FSOrdnanceCryogenicCoolingPumps-kicker");
        Apply("FSOrdnanceSubAtomicParticleIonizers", isEnergy, 0.03f);
        Apply("FSOrdnanceSubAtomicParticleIonizers", isTesla, 0.02f, "FSOrdnanceSubAtomicParticleIonizers-kicker");
        Apply("FSOrdnanceHeavyStepUpTransformers", isEnergy, 0.03f);
        Apply("FSOrdnanceHeavyStepUpTransformers", isTesla, 0.02f, "FSOrdnanceHeavyStepUpTransformers-kicker");
        Apply("FSOrdnanceFocusingCrystalPrisms", isXray, -0.08f);
        Apply("FSOrdnanceOverchargeCapacitors", isTesla, 0.10f);
        Apply("FSOrdnanceXrayCannon", isXray, 0.05f);
        Apply("FSOrdnanceTeslaCannon", isTesla, 0.05f);
        // Flux Tuning (augment, gated on the tesla-charge-speed shop level) is handled in ReconcileAugments.

        if (mult == 1f)
            return;

        charger.AutoRechargeRate *= mult;
        Dirty(weapon, charger);
        _battery.RefreshChargeRate((weapon, null));
    }

    private void ReconcileMisc(EntityUid weapon, FSResearchAppliedComponent tracker, FSWeaponUpgradeStateComponent state,
        bool isL6, bool isMinigun, bool isXray, bool isRpg, bool isTesla)
    {
        var dirty = false;

        if (TryComp<FSXrayRaycastComponent>(weapon, out var xray))
        {
            var d = Delta(tracker, "FSOrdnanceWaveParticleHarnessing", isXray && Unlocked("FSOrdnanceWaveParticleHarnessing") ? 1 : 0);
            if (d != 0)
            {
                xray.MaxDistance *= MathF.Pow(1.08f, d);
                Dirty(weapon, xray);
            }
        }

        var critDelta = 0f;
        critDelta += Delta(tracker, "FSOrdnanceMinigunBallisticCalculators", (isL6 || isMinigun) && Unlocked("FSOrdnanceMinigunBallisticCalculators") ? 1 : 0) * 0.025f;
        critDelta += Delta(tracker, "FSOrdnanceMinigunBallisticCalculators-kicker", isMinigun && Unlocked("FSOrdnanceMinigunBallisticCalculators") ? 1 : 0) * 0.02f;
        critDelta += Delta(tracker, "FSOrdnanceL6ModernAutomaticSaw", isL6 && Unlocked("FSOrdnanceL6ModernAutomaticSaw") ? 1 : 0) * 0.05f;
        if (critDelta != 0f)
        {
            state.CritChance = Math.Clamp(state.CritChance + critDelta, 0f, 1f);
            dirty = true;
        }

        var shredDelta = 0f;
        shredDelta += Delta(tracker, "FSOrdnanceLeadLinedEmitters", isXray && Unlocked("FSOrdnanceLeadLinedEmitters") ? 1 : 0) * 0.05f;
        shredDelta += Delta(tracker, "FSOrdnanceTandemWarheads", isRpg && Unlocked("FSOrdnanceTandemWarheads") ? 1 : 0) * 0.12f;
        if (shredDelta != 0f)
        {
            state.ArmorShredMagnitude = Math.Max(0f, state.ArmorShredMagnitude + shredDelta);
            dirty = true;
        }

        var blastDelta = 0;
        blastDelta += Delta(tracker, "FSOrdnanceTandemWarheads-radius", isRpg && Unlocked("FSOrdnanceTandemWarheads") ? 1 : 0) * -1;
        blastDelta += Delta(tracker, "FSOrdnanceThermobaricPayloads", isRpg && Unlocked("FSOrdnanceThermobaricPayloads") ? 1 : 0) * 2;
        blastDelta += Delta(tracker, "FSOrdnanceRpg7", isRpg && Unlocked("FSOrdnanceRpg7") ? 1 : 0) * 1;
        if (blastDelta != 0)
        {
            state.BlastRadiusBonus = Math.Max(0, state.BlastRadiusBonus + blastDelta);
            dirty = true;
        }

        // Tesla arc range (capstone flourish, distinct from the Ionized Atmospheric Path augment below).
        var arcDelta = Delta(tracker, "FSOrdnanceTeslaCannon-arc", isTesla && Unlocked("FSOrdnanceTeslaCannon") ? 1 : 0);
        if (arcDelta != 0)
        {
            state.TeslaArcRangeBonus += 0.3f * arcDelta;
            dirty = true;
        }

        var chainDelta = Delta(tracker, "FSOrdnanceChainLightningBridging", isTesla && Unlocked("FSOrdnanceChainLightningBridging") ? 1 : 0);
        if (chainDelta != 0)
        {
            state.TeslaChainTargetBonus += chainDelta;
            dirty = true;
        }

        var pierceDelta = Delta(tracker, "FSOrdnanceXrayCannon-pierce", isXray && Unlocked("FSOrdnanceXrayCannon") ? 1 : 0);
        if (pierceDelta != 0)
        {
            state.PierceThreshold += FixedPoint2.New(pierceDelta);
            dirty = true;
        }

        if (dirty)
            Dirty(weapon, state);
    }

    private void ReconcileAugments(EntityUid weapon, FSResearchAppliedComponent tracker, FSWeaponUpgradeStateComponent state,
        Dictionary<string, int> shopLevels, bool isMinigun, bool isXray, bool isTesla)
    {
        var stateDirty = false;

        if (isMinigun)
        {
            // Extended Drum Tuning -> minigun-ammo-drum, +50 capacity/level.
            if (TryComp<BallisticAmmoProviderComponent>(weapon, out var bal))
            {
                var d = Delta(tracker, "FSOrdnanceMinigunExtendedDrumTuning",
                    Unlocked("FSOrdnanceMinigunExtendedDrumTuning") ? shopLevels.GetValueOrDefault("minigun-ammo-drum", 0) : 0);
                if (d != 0)
                {
#pragma warning disable RA0002
                    bal.Capacity = Math.Max(1, bal.Capacity + d * 50);
                    if (d > 0) bal.UnspawnedCount = Math.Min(bal.UnspawnedCount + d * 50, bal.Capacity);
#pragma warning restore RA0002
                    Dirty(weapon, bal);
                }
            }

            // Barrel Overclock Sync -> minigun-fire-rate, +0.2 FR/level.
            if (TryComp<GunComponent>(weapon, out var gun))
            {
                var d = Delta(tracker, "FSOrdnanceMinigunBarrelOverclockSync",
                    Unlocked("FSOrdnanceMinigunBarrelOverclockSync") ? shopLevels.GetValueOrDefault("minigun-fire-rate", 0) : 0);
                if (d != 0)
                {
#pragma warning disable RA0002
                    gun.FireRate += 0.2f * d;
                    gun.FireRateModified = gun.FireRate;
#pragma warning restore RA0002
                    Dirty(weapon, gun);
                }

                // Gyro Matrix Augment -> minigun-accuracy, +2 degrees/level.
                var dAcc = Delta(tracker, "FSOrdnanceMinigunGyroMatrixAugment",
                    Unlocked("FSOrdnanceMinigunGyroMatrixAugment") ? shopLevels.GetValueOrDefault("minigun-accuracy", 0) : 0);
                if (dAcc != 0)
                {
                    var deg = 2.0 * dAcc;
#pragma warning disable RA0002
                    gun.MinAngle = Angle.FromDegrees(Math.Max(0.0, gun.MinAngle.Degrees - deg * 0.5));
                    gun.MaxAngle = Angle.FromDegrees(Math.Max(gun.MinAngle.Degrees, gun.MaxAngle.Degrees - deg * 0.2));
                    gun.AngleIncrease = Angle.FromDegrees(Math.Max(0.0, gun.AngleIncrease.Degrees - deg * 0.3));
                    gun.MinAngleModified = gun.MinAngle;
                    gun.MaxAngleModified = gun.MaxAngle;
                    gun.AngleIncreaseModified = gun.AngleIncrease;
#pragma warning restore RA0002
                    Dirty(weapon, gun);
                }
            }

            // Iron Beast Bracing -> minigun-iron-beast, flat 20% -> 25% once owned.
            var dIron = Delta(tracker, "FSOrdnanceMinigunIronBeastBracing",
                Unlocked("FSOrdnanceMinigunIronBeastBracing") && shopLevels.GetValueOrDefault("minigun-iron-beast", 0) >= 1 ? 1 : 0);
            if (dIron != 0)
                EnsureComp<FSIronBeastComponent>(weapon).ResistBonus += 0.05f * dIron;

            // Core Casting Vats -> minigun-damage, flat +5% once owned.
            var dCore = Delta(tracker, "FSOrdnanceMinigunCoreCastingVats",
                Unlocked("FSOrdnanceMinigunCoreCastingVats") && shopLevels.GetValueOrDefault("minigun-damage", 0) >= 1 ? 1 : 0);
            if (dCore != 0)
            {
                state.DamageMultiplier += 0.05f * dCore;
                stateDirty = true;
            }

            // Knockback Rage -> minigun-knockback, +5% pushback/level.
            var dKnock = Delta(tracker, "FSOrdnanceMinigunKnockbackRage",
                Unlocked("FSOrdnanceMinigunKnockbackRage") ? shopLevels.GetValueOrDefault("minigun-knockback", 0) : 0);
            if (dKnock != 0)
            {
                state.KnockbackResearchForceBonus += 0.05f * dKnock;
                stateDirty = true;
            }
        }

        if (isXray)
        {
            // Isotope Synthesiser -> xray-dot, +5% ticking damage/level.
            var dDot = Delta(tracker, "FSOrdnanceIsotopeSynthesiser",
                Unlocked("FSOrdnanceIsotopeSynthesiser") ? shopLevels.GetValueOrDefault("xray-dot", 0) : 0);
            if (dDot != 0)
            {
                state.RadiationCoatingResearchBonus += 0.05f * dDot;
                stateDirty = true;
            }

            // Collimator Flanges -> xray-accuracy, +2 degrees/level (damage penalty handled in Hook B).
            if (TryComp<GunComponent>(weapon, out var xrayGun))
            {
                var dColl = Delta(tracker, "FSOrdnanceCollimatorFlanges",
                    Unlocked("FSOrdnanceCollimatorFlanges") ? shopLevels.GetValueOrDefault("xray-accuracy", 0) : 0);
                if (dColl != 0)
                {
                    var deg = 2.0 * dColl;
#pragma warning disable RA0002
                    xrayGun.MinAngle = Angle.FromDegrees(Math.Max(0.0, xrayGun.MinAngle.Degrees - deg * 0.5));
                    xrayGun.MaxAngle = Angle.FromDegrees(Math.Max(xrayGun.MinAngle.Degrees, xrayGun.MaxAngle.Degrees - deg * 0.2));
                    xrayGun.MinAngleModified = xrayGun.MinAngle;
                    xrayGun.MaxAngleModified = xrayGun.MaxAngle;
#pragma warning restore RA0002
                    Dirty(weapon, xrayGun);
                }

                // Frequency Sync -> xray-fire-rate, +0.1 FR/level.
                var dFreq = Delta(tracker, "FSOrdnanceFrequencySync",
                    Unlocked("FSOrdnanceFrequencySync") ? shopLevels.GetValueOrDefault("xray-fire-rate", 0) : 0);
                if (dFreq != 0)
                {
#pragma warning disable RA0002
                    xrayGun.FireRate += 0.1f * dFreq;
                    xrayGun.FireRateModified = xrayGun.FireRate;
#pragma warning restore RA0002
                    Dirty(weapon, xrayGun);
                }
            }

            // Density Tuning -> xray-pierce, +1 pierce once maxed (level 3).
            var dDensity = Delta(tracker, "FSOrdnanceDensityTuning",
                Unlocked("FSOrdnanceDensityTuning") && shopLevels.GetValueOrDefault("xray-pierce", 0) >= 3 ? 1 : 0);
            if (dDensity != 0)
            {
                state.PierceThreshold += FixedPoint2.New(dDensity);
                stateDirty = true;
            }

            // Focal Overdrive -> xray-damage, flat +5% once owned.
            var dFocal = Delta(tracker, "FSOrdnanceFocalOverdrive",
                Unlocked("FSOrdnanceFocalOverdrive") && shopLevels.GetValueOrDefault("xray-damage", 0) >= 1 ? 1 : 0);
            if (dFocal != 0)
            {
                state.DamageMultiplier += 0.05f * dFocal;
                stateDirty = true;
            }
        }

        if (isTesla)
        {
            // Overclocked Timing -> tesla-fire-rate, flat +0.5 FR (~+10%) once owned.
            if (TryComp<GunComponent>(weapon, out var teslaGun))
            {
                var dOver = Delta(tracker, "FSOrdnanceOverclockedTiming",
                    Unlocked("FSOrdnanceOverclockedTiming") && shopLevels.GetValueOrDefault("tesla-fire-rate", 0) >= 1 ? 1 : 0);
                if (dOver != 0)
                {
#pragma warning disable RA0002
                    teslaGun.FireRate += 0.5f * dOver;
                    teslaGun.FireRateModified = teslaGun.FireRate;
#pragma warning restore RA0002
                    Dirty(weapon, teslaGun);
                }
            }

            // Dense Lithium Storage -> tesla-energy-capacity, +10 effective shots once owned.
            if (TryComp<BatteryComponent>(weapon, out var battery) && TryComp<BatteryAmmoProviderComponent>(weapon, out var batAmmo))
            {
                var dLithium = Delta(tracker, "FSOrdnanceDenseLithiumStorage",
                    Unlocked("FSOrdnanceDenseLithiumStorage") && shopLevels.GetValueOrDefault("tesla-energy-capacity", 0) >= 1 ? 1 : 0);
                if (dLithium != 0 && batAmmo.FireCost > 0f)
                {
                    var currentShots = battery.MaxCharge / batAmmo.FireCost;
                    var newFireCost = battery.MaxCharge / Math.Max(1f, currentShots + 10f * dLithium);
#pragma warning disable RA0002
                    batAmmo.FireCost = Math.Max(1f, newFireCost);
#pragma warning restore RA0002
                    Dirty(weapon, batAmmo);
                }
            }

            // Ionized Atmospheric Path -> tesla-arc-range, +0.5m/level (same field the shop upgrade uses).
            var dArc = Delta(tracker, "FSOrdnanceIonizedAtmosphericPath",
                Unlocked("FSOrdnanceIonizedAtmosphericPath") ? shopLevels.GetValueOrDefault("tesla-arc-range", 0) : 0);
            if (dArc != 0)
            {
                state.TeslaArcRangeBonus += 0.5f * dArc;
                stateDirty = true;
            }

            // Flux Tuning -> tesla-charge-speed, flat +25% recharge once owned.
            var fluxTarget = Unlocked("FSOrdnanceFluxTuning") && shopLevels.GetValueOrDefault("tesla-charge-speed", 0) >= 1 ? 1 : 0;
            var fluxDelta = Delta(tracker, "FSOrdnanceFluxTuning-augment", fluxTarget);
            if (fluxDelta != 0 && TryComp<BatterySelfRechargerComponent>(weapon, out var fluxCharger))
            {
                fluxCharger.AutoRechargeRate *= MathF.Pow(1.25f, fluxDelta);
                Dirty(weapon, fluxCharger);
                _battery.RefreshChargeRate((weapon, null));
            }

            // Singularity Harnessing -> tesla-pull-strength, flat +15% pull once owned.
            var dSing = Delta(tracker, "FSOrdnanceSingularityHarnessing",
                Unlocked("FSOrdnanceSingularityHarnessing") && shopLevels.GetValueOrDefault("tesla-pull-strength", 0) >= 1 ? 1 : 0);
            if (dSing != 0)
                EnsureComp<FSGravitonCoreComponent>(weapon).ResearchMultiplier *= MathF.Pow(1.15f, dSing);
        }

        if (stateDirty)
            Dirty(weapon, state);
    }
}
