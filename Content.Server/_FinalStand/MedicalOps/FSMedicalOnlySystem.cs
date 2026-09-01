using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Interaction;
using Content.Shared.Medical;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

// Mirrors FSRCDEngineerOnlySystem: medical gear refuses to work for anyone outside the department.
public sealed partial class FSMedicalOnlySystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private static readonly ProtoId<DepartmentPrototype> MedicalDept = "Medical";

    public override void Initialize()
    {
        base.Initialize();

        // Explicitly ordered ahead of everything it gates - subscription order between systems is
        // otherwise undefined, and a late refusal would arrive after the beam had already linked.
        SubscribeLocalEvent<FSMedicalOnlyComponent, AfterInteractEvent>(OnAfterInteract,
            before: [typeof(FSMediGunSystem), typeof(SharedDefibrillatorSystem)]);
    }

    private void OnAfterInteract(EntityUid uid, FSMedicalOnlyComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || IsMedical(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("fs-medical-only"), uid, args.User, PopupType.Medium);
    }

    public bool IsMedical(EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return false;

        if (!_roles.MindHasRole(mindId, typeof(JobRoleComponent), out var jobRole))
            return false;

        if (jobRole.Value.Comp.JobPrototype is not { } jobProtoId)
            return false;

        return _proto.TryIndex<DepartmentPrototype>(MedicalDept, out var department)
               && department.Roles.Contains(jobProtoId);
    }
}
