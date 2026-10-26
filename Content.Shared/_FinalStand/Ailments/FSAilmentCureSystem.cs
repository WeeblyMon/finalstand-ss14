using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._FinalStand.Ailments;

public sealed partial class FSAilmentCureSystem : EntitySystem
{
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private FSMedicalBonusSystem _medicalBonus = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stacks = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSplintComponent, UseInHandEvent>(OnSplintUse);
        SubscribeLocalEvent<FSSplintComponent, AfterInteractEvent>(OnSplintAfterInteract);
        SubscribeLocalEvent<FSSplintComponent, FSSplintDoAfterEvent>(OnSplintDoAfter);
    }

    public bool Cures(EntityUid item, EntityUid patient)
    {
        return TryComp<FSAilmentCureComponent>(item, out var cure) && _ailments.Has(patient, cure.Slot);
    }

    public void Treated(EntityUid item, EntityUid patient, EntityUid user, float stoppedBleed)
    {
        if (!TryComp<FSAilmentCureComponent>(item, out var cure))
            return;

        if (cure.Ward is { } ward)
            _ailments.ApplyWard(patient, ward);

        if (cure.Slot != FSAilmentSlot.Bleeding)
            _ailments.Cure(patient, cure.Slot, user);
        else if (stoppedBleed > 0f)
            _ailments.ReportBleedStopped(patient, stoppedBleed, user);
    }

    public TimeSpan GetTreatmentDelay(EntityUid user, EntityUid patient, TimeSpan delay)
    {
        delay *= _medicalBonus.GetDelayMultiplier(user, FSMedicalBonusCategory.TreatmentSpeed);
        if (user != patient && TryComp<FSSteadyHandsComponent>(user, out var hands))
            delay *= hands.DelayMultiplier;

        return delay;
    }

    private void OnSplintUse(Entity<FSSplintComponent> ent, ref UseInHandEvent args)
    {
        if (!args.Handled)
            args.Handled = TrySplint(ent, args.User, args.User);
    }

    private void OnSplintAfterInteract(Entity<FSSplintComponent> ent, ref AfterInteractEvent args)
    {
        if (!args.Handled && args.CanReach && args.Target is { } target)
            args.Handled = TrySplint(ent, target, args.User);
    }

    private bool TrySplint(Entity<FSSplintComponent> splint, EntityUid patient, EntityUid user)
    {
        if (!HasComp<FSAilmentStateComponent>(patient))
            return false;

        if (user != patient && !_interaction.InRangeUnobstructed(user, patient, popup: true))
            return false;

        if (!_ailments.IsBroken(patient))
        {
            _popup.PopupClient(Loc.GetString("fs-splint-nothing", ("target", Identity.Entity(patient, EntityManager))), patient, user);
            return true;
        }

        var self = user == patient;
        if (!self)
            _popup.PopupEntity(Loc.GetString("fs-splint-start", ("user", Identity.Entity(user, EntityManager))), patient, patient, PopupType.Medium);

        var delay = self ? splint.Comp.SelfDelay : GetTreatmentDelay(user, patient, splint.Comp.Delay);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, delay, new FSSplintDoAfterEvent(), splint, target: patient, used: splint)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnWeightlessMove = false,
        });

        return true;
    }

    private void OnSplintDoAfter(Entity<FSSplintComponent> ent, ref FSSplintDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } patient)
            return;

        args.Handled = true;
        if (!_ailments.Splint(patient, ent.Comp.SplintedDuration))
            return;

        _audio.PlayPredicted(ent.Comp.Sound, patient, args.User);
        var message = args.User == patient
            ? Loc.GetString("fs-splint-done-self")
            : Loc.GetString("fs-splint-done", ("target", Identity.Entity(patient, EntityManager)));
        _popup.PopupClient(message, patient, args.User);

        if (TryComp<StackComponent>(ent, out var stack))
            _stacks.ReduceCount((ent.Owner, stack), 1);
        else
            PredictedQueueDel(ent.Owner);
    }
}
