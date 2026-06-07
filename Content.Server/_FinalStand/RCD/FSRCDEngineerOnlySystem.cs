using Content.Shared._FinalStand.RCD;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.RCD;

public sealed class FSRCDEngineerOnlySystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private static readonly ProtoId<DepartmentPrototype> EngineeringDept = "Engineering";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RCDComponent, ComponentStartup>(OnRCDStartup);
        SubscribeLocalEvent<FSEngineerOnlyRCDComponent, AfterInteractEvent>(OnAfterInteract,
            before: [typeof(RCDSystem)]);
    }

    private void OnRCDStartup(EntityUid uid, RCDComponent comp, ComponentStartup args)
    {
        EnsureComp<FSEngineerOnlyRCDComponent>(uid);
    }

    private void OnAfterInteract(EntityUid uid, FSEngineerOnlyRCDComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (IsEngineer(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity("Can only be used by Engineers", uid, args.User, PopupType.Medium);
    }

    private bool IsEngineer(EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return false;
        if (!_roles.MindHasRole(mindId, typeof(JobRoleComponent), out var jobRole))
            return false;
        var jobProtoId = jobRole.Value.Comp.JobPrototype;
        if (jobProtoId == null)
            return false;
        if (!_proto.TryIndex<DepartmentPrototype>(EngineeringDept, out var engDept))
            return false;
        return engDept.Roles.Contains(jobProtoId.Value);
    }
}
