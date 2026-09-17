using System.Collections.Frozen;
using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared.Medical;
using Content.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

// Medical research buys numbers, not just recipes. Values are pushed onto live components when a
// node completes and onto new ones at MapInit, so a node bought mid-wave reaches the medigun already
// in someone's hands. Mirrors FSResearchStaticGrantSystem's Unlocked(nodeId) idiom.
public sealed class FSMedicalUpgradeSystem : EntitySystem
{
    [Dependency] private FSMedicalResearchSystem _research = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public const string ExtendedOptics = "FSMedicalExtendedOptics";
    public const string HaemostaticBeam = "FSMedicalHaemostaticBeam";
    public const string FocusedEmitters = "FSMedicalFocusedEmitters";
    public const string RapidCycling = "FSMedicalRapidCycling";
    public const string CapacitorRecovery = "FSMedicalCapacitorRecovery";
    public const string CellEfficiency = "FSMedicalCellEfficiency";
    public const string DefibrillatorOutput = "FSMedicalDefibrillatorOutput";

    public const string AutoclaveKit = "FSMedicalAutoclaveKit";
    public const string ReinforcedCanvas = "FSMedicalReinforcedCanvas";
    public const string BoneWelder = "FSMedicalBoneWelder";
    public const string RecoveryUplink = "FSMedicalRecoveryUplink";
    public const string SterileField = "FSMedicalSterileField";

    public const string VolatileSuspension = "FSMedicalVolatileSuspension";
    public const string StabilisedAerosol = "FSMedicalStabilisedAerosol";

    public const string LongRangeCollectors = "FSMedicalLongRangeCollectors";
    public const string WideSpectrumRendering = "FSMedicalWideSpectrumRendering";
    public const string CryoStowage = "FSMedicalCryoStowage";
    public const string HighFlowManifold = "FSMedicalHighFlowManifold";

    public const string StandingOrders = "FSMedicalStandingOrders";
    public const string RapidMobilisation = "FSMedicalRapidMobilisation";
    public const string ExtendedProtocol = "FSMedicalExtendedProtocol";
    public const string TriageDoctrine = "FSMedicalTriageDoctrine";
    public const string MassCasualtyReadiness = "FSMedicalMassCasualtyReadiness";
    public const string DepartmentDividend = "FSMedicalDepartmentDividend";

    // Every node id this system reads. MedicalResearchContentTest asserts each resolves, because a
    // typo here is silently never true rather than an error.
    public static readonly string[] AllNodes =
    [
        ExtendedOptics, HaemostaticBeam, FocusedEmitters, RapidCycling, CapacitorRecovery,
        CellEfficiency, DefibrillatorOutput,
        AutoclaveKit, ReinforcedCanvas, BoneWelder, RecoveryUplink, SterileField,
        VolatileSuspension, StabilisedAerosol,
        LongRangeCollectors, WideSpectrumRendering, CryoStowage, HighFlowManifold,
        StandingOrders, RapidMobilisation, ExtendedProtocol, TriageDoctrine, MassCasualtyReadiness,
        DepartmentDividend,
    ];

    private static readonly FrozenSet<string> NodeSet = AllNodes.ToFrozenSet();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnNodeCompleted);

        SubscribeLocalEvent<FSMediGunComponent, MapInitEvent>(OnMediGunInit);
        SubscribeLocalEvent<FSHarvestSatchelComponent, MapInitEvent>(OnSatchelInit);
        SubscribeLocalEvent<FSSplashFlaskComponent, MapInitEvent>(OnFlaskInit);
        SubscribeLocalEvent<FSBoneStaplerComponent, MapInitEvent>(OnStaplerInit);
        SubscribeLocalEvent<FSFieldHospitalComponent, MapInitEvent>(OnFieldHospitalInit);
        SubscribeLocalEvent<FSEmergencyDefibComponent, MapInitEvent>(OnDefibInit);
        SubscribeLocalEvent<FSSyringeFillerComponent, MapInitEvent>(OnFillerInit);
    }

    public bool Unlocked(string nodeId) => _research.IsNodeUnlocked(nodeId);

    private void OnNodeCompleted(FSResearchNodeCompletedEvent ev)
    {
        if (!NodeSet.Contains(ev.NodeId))
            return;

        ApplyToAll();
    }

    private void ApplyToAll()
    {
        var guns = EntityQueryEnumerator<FSMediGunComponent>();
        while (guns.MoveNext(out var uid, out var gun))
            ApplyMediGun((uid, gun));

        var satchels = EntityQueryEnumerator<FSHarvestSatchelComponent>();
        while (satchels.MoveNext(out var uid, out var satchel))
            ApplySatchel((uid, satchel));

        var flasks = EntityQueryEnumerator<FSSplashFlaskComponent>();
        while (flasks.MoveNext(out var uid, out var flask))
            ApplyFlask((uid, flask));

        var staplers = EntityQueryEnumerator<FSBoneStaplerComponent>();
        while (staplers.MoveNext(out var uid, out var stapler))
            ApplyStapler((uid, stapler));

        var tents = EntityQueryEnumerator<FSFieldHospitalComponent>();
        while (tents.MoveNext(out var uid, out var tent))
            ApplyFieldHospital((uid, tent));

        var defibs = EntityQueryEnumerator<FSEmergencyDefibComponent>();
        while (defibs.MoveNext(out var uid, out var defib))
            ApplyDefib((uid, defib));

        var fillers = EntityQueryEnumerator<FSSyringeFillerComponent>();
        while (fillers.MoveNext(out var uid, out var filler))
            ApplyFiller((uid, filler));
    }

    private void OnMediGunInit(Entity<FSMediGunComponent> ent, ref MapInitEvent args) => ApplyMediGun(ent);
    private void OnSatchelInit(Entity<FSHarvestSatchelComponent> ent, ref MapInitEvent args) => ApplySatchel(ent);
    private void OnFlaskInit(Entity<FSSplashFlaskComponent> ent, ref MapInitEvent args) => ApplyFlask(ent);
    private void OnStaplerInit(Entity<FSBoneStaplerComponent> ent, ref MapInitEvent args) => ApplyStapler(ent);
    private void OnFieldHospitalInit(Entity<FSFieldHospitalComponent> ent, ref MapInitEvent args) => ApplyFieldHospital(ent);
    private void OnDefibInit(Entity<FSEmergencyDefibComponent> ent, ref MapInitEvent args) => ApplyDefib(ent);
    private void OnFillerInit(Entity<FSSyringeFillerComponent> ent, ref MapInitEvent args) => ApplyFiller(ent);

    // The soft cap is never touched. Range, blood restoration, link count and tick rate only.
    private void ApplyMediGun(Entity<FSMediGunComponent> ent)
    {
        var c = ent.Comp;
        c.BaseMaxRange ??= c.MaxRange;
        c.BaseBleedingAmountModifier ??= c.BleedingAmountModifier;
        c.BaseMaxLinksAmount ??= c.MaxLinksAmount;
        c.BaseFrequency ??= c.Frequency;

        c.MaxRange = c.BaseMaxRange.Value + (Unlocked(ExtendedOptics) ? 3f : 0f);
        c.BleedingAmountModifier = c.BaseBleedingAmountModifier.Value + (Unlocked(HaemostaticBeam) ? 2 : 0);
        c.MaxLinksAmount = c.BaseMaxLinksAmount.Value + (Unlocked(FocusedEmitters) ? 1 : 0);
        c.Frequency = c.BaseFrequency.Value * (Unlocked(RapidCycling) ? 0.7f : 1f);

        c.BaseBatteryWithdraw ??= c.BatteryWithdraw;
        c.BatteryWithdraw = c.BaseBatteryWithdraw.Value * (Unlocked(CellEfficiency) ? 0.6f : 1f);

        Dirty(ent);
    }

    // The per-wave ceiling rises but never disappears; it is what stops kills funding kills.
    private void ApplySatchel(Entity<FSHarvestSatchelComponent> ent)
    {
        var c = ent.Comp;
        c.BaseRange ??= c.Range;
        c.BasePerKill ??= c.PerKill;
        c.BasePerWaveCap ??= c.PerWaveCap;

        c.Range = c.BaseRange.Value + (Unlocked(LongRangeCollectors) ? 4f : 0f);
        c.PerKill = c.BasePerKill.Value + (Unlocked(WideSpectrumRendering) ? 2f : 0f);
        c.PerWaveCap = c.BasePerWaveCap.Value * (Unlocked(CryoStowage) ? 1.5f : 1f);

        Dirty(ent);
    }

    private void ApplyFlask(Entity<FSSplashFlaskComponent> ent)
    {
        var c = ent.Comp;
        c.BaseSpreadAmount ??= c.SpreadAmount;
        c.BaseDuration ??= c.Duration;

        c.SpreadAmount = c.BaseSpreadAmount.Value + (Unlocked(VolatileSuspension) ? 3 : 0);
        c.Duration = c.BaseDuration.Value * (Unlocked(StabilisedAerosol) ? 1.8f : 1f);

        Dirty(ent);
    }

    // Repair stays partial at 45 of 60 - research buys the cooldown, never the mend, or surgery dies.
    private void ApplyStapler(Entity<FSBoneStaplerComponent> ent)
    {
        if (!TryComp<UseDelayComponent>(ent.Owner, out var delay))
            return;

        ent.Comp.BaseUseDelay ??= delay.Delay;

        var scale = Unlocked(BoneWelder) ? 30f / 45f : 1f;
        _useDelay.SetLength((ent.Owner, delay), ent.Comp.BaseUseDelay.Value * scale);
    }

    private void ApplyFieldHospital(Entity<FSFieldHospitalComponent> ent)
    {
        if (TryComp<FSArmingDelayComponent>(ent.Owner, out var arming))
        {
            ent.Comp.BaseArmingDelay ??= arming.Delay;
            arming.Delay = ent.Comp.BaseArmingDelay.Value * (Unlocked(AutoclaveKit) ? 0.5f : 1f);
            Dirty(ent.Owner, arming);
        }

        if (TryComp<FSDeployableLifetimeComponent>(ent.Owner, out var lifetime))
        {
            ent.Comp.BaseLifetime ??= lifetime.Lifetime;
            lifetime.Lifetime = ent.Comp.BaseLifetime.Value * (Unlocked(ReinforcedCanvas) ? 2f : 1f);
            Dirty(ent.Owner, lifetime);
        }

        // SharedSurgerySystem.Steps reads SpeedModifier off whatever the patient is strapped to.
        if (TryComp<OperatingTableComponent>(ent.Owner, out var table))
        {
            ent.Comp.BaseSurgerySpeed ??= table.SpeedModifier;
            table.SpeedModifier = ent.Comp.BaseSurgerySpeed.Value * (Unlocked(SterileField) ? 1.5f : 1f);
            Dirty(ent.Owner, table);
        }
    }

    // The lockout carries the defib's whole balance, so this is the one number research may buy.
    private void ApplyDefib(Entity<FSEmergencyDefibComponent> ent)
    {
        if (!TryComp<DefibrillatorComponent>(ent.Owner, out var defib))
            return;

        ent.Comp.BaseZapDelay ??= defib.ZapDelay;
        defib.ZapDelay = ent.Comp.BaseZapDelay.Value * (Unlocked(CapacitorRecovery) ? 40f / 60f : 1f);
        Dirty(ent.Owner, defib);

        ent.Comp.BaseReviveHealthFraction ??= ent.Comp.ReviveHealthFraction;
        ent.Comp.ReviveHealthFraction =
            ent.Comp.BaseReviveHealthFraction.Value + (Unlocked(DefibrillatorOutput) ? 0.15f : 0f);
    }

    // The filler is station equipment, so this is throughput for the whole department at once.
    private void ApplyFiller(Entity<FSSyringeFillerComponent> ent)
    {
        ent.Comp.BaseUnitsPerSecond ??= ent.Comp.UnitsPerSecond;
        ent.Comp.UnitsPerSecond =
            ent.Comp.BaseUnitsPerSecond.Value * (Unlocked(HighFlowManifold) ? 2.25f : 1f);

        Dirty(ent);
    }

}
