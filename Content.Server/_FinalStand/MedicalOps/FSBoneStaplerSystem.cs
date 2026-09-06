using Content.Shared._FinalStand.Medical;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Body;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSBoneStaplerSystem : EntitySystem
{
    [Dependency] private OrganLookupSystem _organs = default!;
    [Dependency] private TraumaSystem _trauma = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;

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

        if (!TryFindWorstBone(target, body, out var bone, out var boneComp))
        {
            _popup.PopupEntity(Loc.GetString("fs-bone-stapler-nothing-broken"), ent.Owner, args.User);
            return;
        }

        var repaired = FixedPoint2.Min(boneComp.IntegrityCap, boneComp.BoneIntegrity + ent.Comp.Repair);
        _trauma.SetBoneIntegrity(bone, repaired, boneComp);
        _trauma.UpdateBodyBoneAlert(target, body);

        _useDelay.TryResetDelay(ent.Owner);
        _popup.PopupEntity(Loc.GetString("fs-bone-stapler-used"), target, args.User);
    }

    private bool TryFindWorstBone(EntityUid target, BodyComponent body, out EntityUid bone, out BoneComponent boneComp)
    {
        bone = default;
        boneComp = default!;

        var lowest = FixedPoint2.MaxValue;

        foreach (var (organ, _) in _organs.GetBodyOrgans((target, body)))
        {
            if (!TryComp<WoundableComponent>(organ, out var woundable))
                continue;

            foreach (var contained in woundable.Bone.ContainedEntities)
            {
                if (!TryComp<BoneComponent>(contained, out var candidate)
                    || candidate.BoneIntegrity >= candidate.IntegrityCap
                    || candidate.BoneIntegrity >= lowest)
                    continue;

                lowest = candidate.BoneIntegrity;
                bone = contained;
                boneComp = candidate;
            }
        }

        return lowest != FixedPoint2.MaxValue;
    }
}
