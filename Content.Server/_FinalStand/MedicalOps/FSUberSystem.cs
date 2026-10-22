using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Player;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

// UberCharger: healing builds charge, the action spends it on a short window of invulnerability.
public sealed partial class FSUberSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private FSMedicalRosterSystem _roster = default!;
    [Dependency] private FSUberSharedSystem _uberShared = default!;

    public const float FullCharge = 100f;

    // How long a dropped patient stays covered, so a beam that flickers off and back doesn't strip it.
    private static readonly TimeSpan LinkLossGrace = TimeSpan.FromSeconds(0.5);

    private static readonly SoundSpecifier ReadySound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");
    private static readonly SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
    private static readonly SoundSpecifier EndSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");
    private static readonly SoundSpecifier LoopSound = new SoundPathSpecifier("/Audio/_FinalStand/Effects/singularity_hum.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSMediGunComponent, FSMediGunUberActionEvent>(OnUberAction);
        SubscribeLocalEvent<FSMediGunComponent, ComponentShutdown>(OnGunShutdown);
        SubscribeNetworkEvent<FSUberActivateMessage>(OnActivateMessage);
    }

    private void OnActivateMessage(FSUberActivateMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !_hands.TryGetActiveItem(user, out var held)
            || !TryComp<FSMediGunComponent>(held, out var gun)
            || gun.Variant != FSMediGunVariant.UberCharger)
            return;

        TryDeploy((held.Value, gun), user);
    }

    // The hum loops on the medic, so a gun deleted mid-uber would otherwise leave it playing.
    private void OnGunShutdown(Entity<FSMediGunComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.UberLoop = _audio.Stop(ent.Comp.UberLoop);
    }

    public void AddCharge(Entity<FSMediGunComponent> gun, float amount)
    {
        var comp = gun.Comp;
        if (comp.Variant != FSMediGunVariant.UberCharger || comp.UberActive || amount <= 0f)
            return;

        var before = comp.UberCharge;
        comp.UberCharge = MathF.Min(FullCharge, comp.UberCharge + amount);
        if ((int) comp.UberCharge == (int) before)
            return;

        Dirty(gun);

        if (before < FullCharge && comp.UberCharge >= FullCharge && comp.ParentEntity is { } medic)
        {
            _audio.PlayEntity(ReadySound, medic, medic);
            _popup.PopupEntity(Loc.GetString("fs-uber-ready"), medic, medic, PopupType.Medium);
        }
    }

    public void Cover(Entity<FSMediGunComponent> gun, EntityUid patient)
    {
        if (gun.Comp.UberEndTime is not { } end)
            return;

        _uberShared.Cover(patient, gun, end, gun.Comp.UberDuration, gun.Comp.BeamColor, gun.Comp.UberInfiniteStamina);
    }

    // A patient keeps the cover only while the beam holds them, so one uber can't be passed round the whole crew.
    public void Uncover(Entity<FSMediGunComponent> gun, EntityUid patient)
    {
        if (patient == gun.Comp.ParentEntity)
            return;

        _uberShared.EndCover(patient, gun, _timing.CurTime + LinkLossGrace);

        // A medic's own uber covers them once, when it goes up, so a longer one from this beam may have replaced it.
        var guns = EntityQueryEnumerator<FSMediGunComponent>();
        while (guns.MoveNext(out var uid, out var other))
        {
            if (other.UberMedic == patient && other.UberActive)
                Cover((uid, other), patient);
        }
    }

    private void OnUberAction(Entity<FSMediGunComponent> ent, ref FSMediGunUberActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryDeploy(ent, args.Performer);
    }

    private void TryDeploy(Entity<FSMediGunComponent> ent, EntityUid medic)
    {
        var comp = ent.Comp;
        if (comp.UberActive)
            return;

        // Linking is medical-only; a charged gun handed to someone else can't be spent by them either.
        if (HasComp<FSMedicalOnlyComponent>(ent) && !_roster.IsMedical(medic))
        {
            _popup.PopupEntity(Loc.GetString("fs-medical-only"), medic, medic, PopupType.Medium);
            return;
        }

        if (comp.UberCharge < FullCharge)
        {
            _popup.PopupEntity(Loc.GetString("fs-uber-not-ready", ("charge", (int) comp.UberCharge)), medic, medic);
            return;
        }

        comp.UberEndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.UberDuration);
        comp.UberMedic = medic;
        comp.ParentEntity ??= medic;
        Dirty(ent);

        Cover(ent, medic);
        foreach (var patient in comp.HealedEntities)
            Cover(ent, patient);

        _audio.PlayPvs(DeploySound, medic);
        comp.UberLoop = _audio.PlayPvs(LoopSound, medic, AudioParams.Default.WithLoop(true).WithVolume(-12f))?.Entity;
        _popup.PopupEntity(Loc.GetString("fs-uber-deployed"), medic, PopupType.Large);
        RaiseNetworkEvent(new FSUberDeployedEvent(GetNetEntity(medic), comp.BeamColor), Filter.Pvs(medic));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var guns = EntityQueryEnumerator<FSMediGunComponent>();
        while (guns.MoveNext(out var uid, out var gun))
        {
            if (gun.UberEndTime is not { } end)
                continue;

            if (now >= end)
            {
                gun.UberEndTime = null;
                gun.UberMedic = null;
                gun.UberCharge = 0f;
                gun.UberLoop = _audio.Stop(gun.UberLoop);
                Dirty(uid, gun);
                if (gun.ParentEntity is { } medic)
                    _audio.PlayEntity(EndSound, medic, medic);
                continue;
            }

            var remaining = (float) ((end - now).TotalSeconds / gun.UberDuration) * FullCharge;
            if ((int) remaining == (int) gun.UberCharge)
                continue;

            gun.UberCharge = remaining;
            Dirty(uid, gun);
        }

        var ubered = EntityQueryEnumerator<FSUberedComponent>();
        while (ubered.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.EndTime)
                RemCompDeferred<FSUberedComponent>(uid);
        }
    }
}
