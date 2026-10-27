using Content.Shared._FinalStand.Ailments;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Medical;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSCardiovertSystem : EntitySystem
{
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private FSMedicalBonusSystem _medicalBonus = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

    public const string DelayId = "cardiovert";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSEmergencyDefibComponent, AfterInteractEvent>(OnAfterInteract, before: [typeof(SharedDefibrillatorSystem)]);
        SubscribeLocalEvent<FSEmergencyDefibComponent, FSCardiovertDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<FSEmergencyDefibComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target is not { } target
            || !HasComp<FSAilmentStateComponent>(target)
            || !_mobState.IsAlive(target))
        {
            return;
        }

        args.Handled = true;

        if (!_ailments.Has(target, FSAilmentSlot.Heart))
        {
            _popup.PopupClient(Loc.GetString("fs-cardiovert-fine"), target, args.User);
            return;
        }

        if (_useDelay.IsDelayed(ent.Owner, DelayId))
        {
            _popup.PopupClient(Loc.GetString("fs-cardiovert-recharging"), ent.Owner, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.CardiovertDelay, new FSCardiovertDoAfterEvent(), ent, target: target, used: ent)
        {
            NeedHand = true,
            BreakOnMove = true,
        });
    }

    private void OnDoAfter(Entity<FSEmergencyDefibComponent> ent, ref FSCardiovertDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;
        if (!_ailments.Cure(target, FSAilmentSlot.Heart, args.User))
            return;

        var cooldown = ent.Comp.CardiovertCooldown * _medicalBonus.GetDelayMultiplier(args.User, FSMedicalBonusCategory.DefibCooldown);
        _useDelay.SetLength(ent.Owner, cooldown, DelayId);
        _useDelay.TryResetDelay(ent.Owner, id: DelayId);

        _audio.PlayPredicted(ent.Comp.CardiovertSound, target, args.User);
        _popup.PopupClient(Loc.GetString("fs-cardiovert-done", ("target", Identity.Entity(target, EntityManager))), target, args.User);
    }
}
