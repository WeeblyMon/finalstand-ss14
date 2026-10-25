using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSBoneStaplerSystem : EntitySystem
{
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private FSMedicalBonusSystem _medicalBonus = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier StapleSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/bone_stapler.ogg");

    private static readonly SoundSpecifier NothingToDoSound =
        new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSBoneStaplerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<FSBoneStaplerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || !HasComp<FSAilmentStateComponent>(target))
            return;

        args.Handled = true;

        if (_useDelay.IsDelayed(ent.Owner))
        {
            _popup.PopupEntity(Loc.GetString("fs-bone-stapler-recharging"), ent.Owner, args.User);
            return;
        }

        string message;
        if (_ailments.Cure(target, FSAilmentSlot.Bone, args.User))
            message = "fs-bone-stapler-bone";
        else if (_ailments.GetBleedLevel(target) >= _ailments.Rules.MajorBleedLevel && _ailments.StopBleeding(target, args.User))
            message = "fs-bone-stapler-bleed";
        else
        {
            _popup.PopupEntity(Loc.GetString("fs-bone-stapler-nothing"), ent.Owner, args.User);
            _audio.PlayPvs(NothingToDoSound, ent.Owner);
            return;
        }

        var cooldown = ent.Comp.Cooldown * _medicalBonus.GetDelayMultiplier(args.User, FSMedicalBonusCategory.DefibCooldown);
        _useDelay.SetLength(ent.Owner, cooldown);
        _useDelay.TryResetDelay(ent.Owner);

        _popup.PopupEntity(Loc.GetString(message, ("target", Identity.Entity(target, EntityManager))), target, args.User);
        _audio.PlayPvs(StapleSound, target);
    }
}
