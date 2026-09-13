using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.MedicalOps;
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

    public const string AutoclaveKit = "FSMedicalAutoclaveKit";
    public const string ReinforcedCanvas = "FSMedicalReinforcedCanvas";
    public const string BoneWelder = "FSMedicalBoneWelder";
    public const string RecoveryUplink = "FSMedicalRecoveryUplink";

    public const string VolatileSuspension = "FSMedicalVolatileSuspension";
    public const string StabilisedAerosol = "FSMedicalStabilisedAerosol";

    public const string LongRangeCollectors = "FSMedicalLongRangeCollectors";
    public const string WideSpectrumRendering = "FSMedicalWideSpectrumRendering";
    public const string CryoStowage = "FSMedicalCryoStowage";

    public const string StandingOrders = "FSMedicalStandingOrders";
    public const string RapidMobilisation = "FSMedicalRapidMobilisation";
    public const string ExtendedProtocol = "FSMedicalExtendedProtocol";
    public const string TriageDoctrine = "FSMedicalTriageDoctrine";
    public const string MassCasualtyReadiness = "FSMedicalMassCasualtyReadiness";

    // Every node id this system reads. MedicalResearchContentTest asserts each resolves, because a
    // typo here is silently never true rather than an error.
    public static readonly string[] AllNodes =
    [
        ExtendedOptics, HaemostaticBeam, FocusedEmitters, RapidCycling, CapacitorRecovery,
        AutoclaveKit, ReinforcedCanvas, BoneWelder, RecoveryUplink,
        VolatileSuspension, StabilisedAerosol,
        LongRangeCollectors, WideSpectrumRendering, CryoStowage,
        StandingOrders, RapidMobilisation, ExtendedProtocol, TriageDoctrine, MassCasualtyReadiness,
    ];

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
    }

    public bool Unlocked(string nodeId) => _research.IsNodeUnlocked(nodeId);

    private void OnNodeCompleted(FSResearchNodeCompletedEvent ev)
    {
        if (Array.IndexOf(AllNodes, ev.NodeId) < 0)
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
    }

    private void OnMediGunInit(Entity<FSMediGunComponent> ent, ref MapInitEvent args) => ApplyMediGun(ent);
    private void OnSatchelInit(Entity<FSHarvestSatchelComponent> ent, ref MapInitEvent args) => ApplySatchel(ent);
    private void OnFlaskInit(Entity<FSSplashFlaskComponent> ent, ref MapInitEvent args) => ApplyFlask(ent);
    private void OnStaplerInit(Entity<FSBoneStaplerComponent> ent, ref MapInitEvent args) => ApplyStapler(ent);
    private void OnFieldHospitalInit(Entity<FSFieldHospitalComponent> ent, ref MapInitEvent args) => ApplyFieldHospital(ent);
    private void OnDefibInit(Entity<FSEmergencyDefibComponent> ent, ref MapInitEvent args) => ApplyDefib(ent);

    // The soft cap is never touched. Range, blood restoration, link count and tick rate only.
    private void ApplyMediGun(Entity<FSMediGunComponent> ent)
    {
        ent.Comp.MaxRange = Unlocked(ExtendedOptics) ? 9f : 6f;
        ent.Comp.BleedingAmountModifier = Unlocked(HaemostaticBeam) ? 5 : 3;
        ent.Comp.MaxLinksAmount = Unlocked(FocusedEmitters) ? 2 : 1;
        ent.Comp.Frequency = Unlocked(RapidCycling) ? 0.7f : 1f;

        Dirty(ent);
    }

    // The per-wave ceiling rises but never disappears; it is what stops kills funding kills.
    private void ApplySatchel(Entity<FSHarvestSatchelComponent> ent)
    {
        ent.Comp.Range = Unlocked(LongRangeCollectors) ? 12f : 8f;
        ent.Comp.PerKill = Unlocked(WideSpectrumRendering) ? 6f : 4f;
        ent.Comp.PerWaveCap = Unlocked(CryoStowage) ? 180f : 120f;

        Dirty(ent);
    }

    private void ApplyFlask(Entity<FSSplashFlaskComponent> ent)
    {
        ent.Comp.SpreadAmount = Unlocked(VolatileSuspension) ? 9 : 6;
        ent.Comp.Duration = Unlocked(StabilisedAerosol) ? 18f : 10f;

        Dirty(ent);
    }

    // Repair stays partial at 45 of 60 - research buys the cooldown, never the mend, or surgery dies.
    private void ApplyStapler(Entity<FSBoneStaplerComponent> ent)
    {
        if (TryComp<UseDelayComponent>(ent.Owner, out var delay))
            _useDelay.SetLength((ent.Owner, delay), TimeSpan.FromSeconds(Unlocked(BoneWelder) ? 30 : 45));
    }

    private void ApplyFieldHospital(Entity<FSFieldHospitalComponent> ent)
    {
        if (TryComp<FSArmingDelayComponent>(ent.Owner, out var arming))
        {
            arming.Delay = Unlocked(AutoclaveKit) ? 3f : 6f;
            Dirty(ent.Owner, arming);
        }

        if (TryComp<FSDeployableLifetimeComponent>(ent.Owner, out var lifetime))
        {
            lifetime.Lifetime = TimeSpan.FromSeconds(Unlocked(ReinforcedCanvas) ? 1200 : 600);
            Dirty(ent.Owner, lifetime);
        }
    }

    // The lockout carries the defib's whole balance, so this is the one number research may buy.
    private void ApplyDefib(Entity<FSEmergencyDefibComponent> ent)
    {
        if (!TryComp<DefibrillatorComponent>(ent.Owner, out var defib))
            return;

        defib.ZapDelay = TimeSpan.FromSeconds(Unlocked(CapacitorRecovery) ? 40 : 60);
        Dirty(ent.Owner, defib);
    }

}
