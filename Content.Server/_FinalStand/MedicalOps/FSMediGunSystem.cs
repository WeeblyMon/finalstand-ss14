using Content.Server._FinalStand.Perks;
using Content.Server.Power.EntitySystems;
using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Timing;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSMediGunSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private BatterySystem _battery = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private FSMedicalBonusSystem _medicalBonus = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private FSCombatMedicSystem _combatMedic = default!;
    [Dependency] private FSOverhealSystem _overheal = default!;
    [Dependency] private FSUberSystem _uber = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    // How far from the cursor a click still counts as aiming at a player.
    private const float ClickAssistRadius = 0.9f;

    private readonly HashSet<Entity<FSFriendlyFireComponent>> _nearClick = new();
    private readonly List<EntityUid> _pulsing = new();

    private EntityQuery<BatteryComponent> _batteryQuery;
    private EntityQuery<DamageableComponent> _damageableQuery;

    public override void Initialize()
    {
        base.Initialize();

        _batteryQuery = GetEntityQuery<BatteryComponent>();
        _damageableQuery = GetEntityQuery<DamageableComponent>();

        SubscribeLocalEvent<FSMediGunComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FSMediGunComponent, EntParentChangedMessage>(OnParentChanged);
        SubscribeLocalEvent<FSMediGunComponent, ItemUnwieldedEvent>(OnUnwielded);
        SubscribeLocalEvent<FSMediGunHealedComponent, EntityTerminatingEvent>(OnPatientTerminating);
    }

    public static bool IsPatient(IEntityManager entMan, EntityUid uid) => entMan.HasComponent<FSFriendlyFireComponent>(uid);

    // A patient deleted mid-beam (gibbed, cryo) drops off every gun healing it, so nothing reads a dead uid.
    private void OnPatientTerminating(Entity<FSMediGunHealedComponent> ent, ref EntityTerminatingEvent args)
    {
        foreach (var source in ent.Comp.Sources)
        {
            if (!TryComp<FSMediGunComponent>(source, out var gun) || !gun.Patients.Remove(ent.Owner))
                continue;

            if (gun.Patients.Count == 0)
                Release((source, gun));
            else
                Dirty(source, gun);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var guns = EntityQueryEnumerator<FSMediGunComponent>();
        while (guns.MoveNext(out var uid, out var gun))
        {
            if (gun.Patients.Count == 0 || now < gun.NextPulse)
                continue;

            Pulse((uid, gun));
        }
    }

    private void Pulse(Entity<FSMediGunComponent> gun)
    {
        _pulsing.Clear();
        _pulsing.AddRange(gun.Comp.Patients);
        foreach (var patient in _pulsing)
        {
            if (!Treat(gun, patient))
                Drop(gun, patient);
        }

        if (gun.Comp.Patients.Count == 0)
        {
            Release(gun);
            return;
        }

        gun.Comp.NextPulse = _timing.CurTime + TimeSpan.FromSeconds(gun.Comp.PulseSeconds);
    }

    private bool Treat(Entity<FSMediGunComponent> gun, EntityUid patient)
    {
        var comp = gun.Comp;

        // A downed or dead medic isn't holding a beam steady - nothing else stops the gun for this.
        if (comp.Medic is not { } medic
            || TerminatingOrDeleted(medic)
            || _mobState.IsIncapacitated(medic)
            || !InRange(gun, patient)
            || !_damageableQuery.TryComp(patient, out var damageable)
            || !DrawCharge(gun))
            return false;

        var clotted = _ailments.Clot(patient, comp.ClotPerSecond * comp.PulseSeconds, medic);
        var mended = Mend(comp, patient, medic);

        var potency = new FSHealingPotencyEvent(1f, patient);
        RaiseLocalEvent(medic, ref potency);

        var scale = GetHealScale((patient, damageable), comp);
        AnnounceSoftCap(gun, patient, scale <= 0f);
        _uber.Cover(gun, patient);

        // Combat Medic counts treatment that landed, not a beam held on someone it can't help.
        if (scale <= 0f)
        {
            if (_overheal.Feed(patient, comp, comp.OverhealPerTick * potency.Multiplier) || clotted || mended)
                _combatMedic.OnHealedAlly(medic, patient);

            _uber.AddCharge(gun, comp.UberChargeWhileCapped);
            return true;
        }

        if (_mobState.IsCritical(patient))
            scale *= _medicalBonus.GetScale(medic, FSMedicalBonusCategory.Stabilisation);

        if (comp.Patients.Count > 1)
            scale *= comp.SplitLinkScale;

        scale *= _ailments.GetIncomingHealScale(patient) * potency.Multiplier;

        // TryChangeDamage reports what was asked for, not what landed, so measure the real change.
        var before = _damageable.GetTotalDamage((patient, damageable)).Float();
        _damageable.TryChangeDamage(patient, comp.HealPerPulse * scale, ignoreResistances: true, interruptsDoAfters: false, origin: medic);

        var restored = before - _damageable.GetTotalDamage((patient, damageable)).Float();
        _uber.AddCharge(gun, restored * comp.UberChargePerHp);

        if (restored > 0f || clotted || mended)
            _combatMedic.OnHealedAlly(medic, patient);

        return true;
    }

    private bool InRange(Entity<FSMediGunComponent> gun, EntityUid patient)
    {
        var from = _xform.GetMapCoordinates(gun.Owner);
        var to = _xform.GetMapCoordinates(patient);
        return from.MapId == to.MapId && (to.Position - from.Position).LengthSquared() <= gun.Comp.Range * gun.Comp.Range;
    }

    private bool DrawCharge(Entity<FSMediGunComponent> gun)
    {
        if (gun.Comp.UberActive || !_batteryQuery.HasComp(gun.Owner))
            return true;

        if (_battery.TryUseCharge(gun.Owner, gun.Comp.ChargePerPulse))
            return true;

        _battery.SetCharge(gun.Owner, 0f);
        return false;
    }

    private void AnnounceSoftCap(Entity<FSMediGunComponent> gun, EntityUid patient, bool capped)
    {
        if (!TryComp<FSMediGunHealedComponent>(patient, out var healed))
            return;

        if (capped && !healed.SoftCapAnnounced)
            _audio.PlayPvs(gun.Comp.CapSound, gun);

        healed.SoftCapAnnounced = capped;
    }

    // Returns whether the beam worked on a bone or burn this pulse.
    private bool Mend(FSMediGunComponent comp, EntityUid patient, EntityUid medic)
    {
        var mended = false;
        var speed = _medicalBonus.GetDelayMultiplier(medic, FSMedicalBonusCategory.TreatmentSpeed);
        if (comp.CanSetBones && _ailments.Has(patient, FSAilmentSlot.Bone))
        {
            var seconds = _ailments.IsBroken(patient) ? comp.BoneSetSeconds : comp.MendingSetSeconds;
            _ailments.AddMend(patient, FSAilmentSlot.Bone, comp.PulseSeconds / (seconds * speed), medic);
            mended = true;
        }

        if (comp.CanClearBurns && _ailments.Has(patient, FSAilmentSlot.Burn))
        {
            _ailments.AddMend(patient, FSAilmentSlot.Burn, comp.PulseSeconds / (comp.BurnClearSeconds * speed), medic);
            mended = true;
        }

        return mended;
    }

    public void ClearTreatable(Entity<FSMediGunComponent> gun, EntityUid patient, EntityUid medic)
    {
        _ailments.StopBleeding(patient, medic, FSCureKind.Mass);
        if (gun.Comp.CanSetBones)
            _ailments.Cure(patient, FSAilmentSlot.Bone, medic, FSCureKind.Mass);

        if (gun.Comp.CanClearBurns)
            _ailments.Cure(patient, FSAilmentSlot.Burn, medic, FSCureKind.Mass);
    }

    public float GetHealScale(Entity<DamageableComponent> patient, FSMediGunComponent comp)
    {
        if (!_thresholds.TryGetThresholdForState(patient.Owner, MobState.Critical, out var crit) || crit is not { } limit || limit <= 0)
            return 1f;

        var health = Math.Clamp(1f - _damageable.GetTotalDamage(patient.AsNullable()).Float() / limit.Float(), 0f, 1f);
        if (health < comp.SoftCapRatio)
            return 1f;

        var room = 1f - comp.SoftCapRatio;
        if (room <= 0f)
            return 0f;

        var scale = MathF.Pow((1f - health) / room, comp.SoftCapFalloff);
        return scale >= comp.MinEffectiveScale ? scale : 0f;
    }

    private void OnAfterInteract(Entity<FSMediGunComponent> gun, ref AfterInteractEvent args)
    {
        if (args.Handled || PickTarget(args.Target, args.ClickLocation, args.User) is not { } target)
            return;

        if (!TryComp<WieldableComponent>(gun, out var wieldable) || !wieldable.Wielded)
        {
            _popup.PopupEntity(Loc.GetString("fs-medigun-needs-wield"), gun, args.User);
            return;
        }

        // The click assist picks anyone near the cursor; a link the beam can't reach would only break next pulse.
        if (_useDelay.IsDelayed(gun.Owner) || !InRange(gun, target))
            return;

        if (gun.Comp.Patients.Contains(target))
            Drop(gun, target);
        else
            Link(gun, target, args.User);

        _useDelay.TryResetDelay(gun.Owner);
        args.Handled = true;
    }

    // Only crew are patients; a miss near a player snaps onto them so fast-moving targets are clickable.
    private EntityUid? PickTarget(EntityUid? clicked, EntityCoordinates click, EntityUid user)
    {
        if (clicked is { } direct && direct != user && IsPatient(EntityManager, direct))
            return direct;

        _nearClick.Clear();
        _lookup.GetEntitiesInRange(click, ClickAssistRadius, _nearClick);
        var clickPos = _xform.ToMapCoordinates(click).Position;

        EntityUid? nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var candidate in _nearClick)
        {
            if (candidate.Owner == user)
                continue;

            var distance = (_xform.GetMapCoordinates(candidate).Position - clickPos).LengthSquared();
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate.Owner;
            }
        }

        return nearest;
    }

    public void Link(Entity<FSMediGunComponent> gun, EntityUid patient, EntityUid medic)
    {
        if (!IsPatient(EntityManager, patient))
            return;

        while (gun.Comp.Patients.Count > 0 && gun.Comp.Patients.Count >= gun.Comp.MaxPatients)
            Drop(gun, gun.Comp.Patients[0]);

        gun.Comp.Patients.Add(patient);
        gun.Comp.Medic = medic;
        gun.Comp.NextPulse = _timing.CurTime + TimeSpan.FromSeconds(gun.Comp.PulseSeconds);
        _uber.Cover(gun, patient);
        Dirty(gun);

        var healed = EnsureComp<FSMediGunHealedComponent>(patient);
        if (!healed.Sources.Contains(gun))
        {
            healed.Sources.Add(gun);
            Dirty(patient, healed);
        }

        _audio.PlayPvs(gun.Comp.LinkSound, gun);
    }

    public void Release(Entity<FSMediGunComponent> gun)
    {
        foreach (var patient in gun.Comp.Patients)
            ForgetSource(gun, patient);

        gun.Comp.Patients.Clear();
        gun.Comp.Medic = null;
        Dirty(gun);
    }

    private void Drop(Entity<FSMediGunComponent> gun, EntityUid patient)
    {
        if (!gun.Comp.Patients.Remove(patient))
            return;

        ForgetSource(gun, patient);
        if (gun.Comp.Patients.Count == 0)
            gun.Comp.Medic = null;

        Dirty(gun);
        _audio.PlayPvs(gun.Comp.LostSound, gun);
    }

    // Runs while the gun still names its medic, so the uber can tell a patient from the medic it covers.
    private void ForgetSource(Entity<FSMediGunComponent> gun, EntityUid patient)
    {
        _uber.Uncover(gun, patient);

        if (!TryComp<FSMediGunHealedComponent>(patient, out var healed) || !healed.Sources.Remove(gun))
            return;

        if (healed.Sources.Count == 0)
            RemComp<FSMediGunHealedComponent>(patient);
        else
            Dirty(patient, healed);
    }

    private void OnUnwielded(Entity<FSMediGunComponent> gun, ref ItemUnwieldedEvent args)
    {
        Release(gun);
    }

    private void OnParentChanged(Entity<FSMediGunComponent> gun, ref EntParentChangedMessage args)
    {
        if (!gun.Comp.BeamFromSelf && args.Transform.ParentUid != gun.Comp.Medic)
            Release(gun);
    }
}
