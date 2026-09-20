using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSBoneStaplerSystem : EntitySystem
{
    [Dependency] private TraumaSystem _trauma = default!;
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
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (_useDelay.IsDelayed(ent.Owner))
            return;

        if (!TryComp<BodyComponent>(target, out var body))
            return;

        args.Handled = true;

        if (!_trauma.TryFindWorstBone(target, body, out var bone, out var boneComp))
        {
            _popup.PopupEntity(Loc.GetString("fs-bone-stapler-nothing-broken"), ent.Owner, args.User);
            _audio.PlayPvs(NothingToDoSound, ent.Owner);
            return;
        }

        var repaired = FixedPoint2.Min(boneComp.IntegrityCap, boneComp.BoneIntegrity + ent.Comp.Repair);
        _trauma.SetBoneIntegrity(bone, repaired, boneComp);
        _trauma.UpdateBodyBoneAlert(target, body);

        _useDelay.TryResetDelay(ent.Owner);

        var limb = Identity.Name(Transform(bone).ParentUid, EntityManager);
        _popup.PopupEntity(Loc.GetString("fs-bone-stapler-used-limb", ("limb", limb)), target, args.User);
        _audio.PlayPvs(StapleSound, target);
    }
}
