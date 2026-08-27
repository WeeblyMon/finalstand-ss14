using Content.Shared._FinalStand.RCD;
using Content.Shared.RCD;
using Robust.Shared.Map.Components;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.RCD;

public sealed partial class FSRCDEngineerOnlySystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<DepartmentPrototype> EngineeringDept = "Engineering";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSEngineerOnlyRCDComponent, AfterInteractEvent>(OnAfterInteract,
            before: [typeof(RCDSystem)]);
    }

    private void OnAfterInteract(EntityUid uid, FSEngineerOnlyRCDComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // No-build markers apply to everyone, engineers included.
        if (IsBuildBlockedHere(uid, args))
        {
            args.Handled = true;
            _popup.PopupEntity(Loc.GetString("fs-rcd-build-blocked"), uid, args.User, PopupType.Medium);
            return;
        }

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

    // Objects (walls, airlocks, windows) are blocked on marked tiles; floor tiles stay legal.
    private bool IsBuildBlockedHere(EntityUid rcd, AfterInteractEvent args)
    {
        if (!TryComp<RCDComponent>(rcd, out var rcdComp)
            || _proto.Index(rcdComp.ProtoId).Mode != RcdMode.ConstructObject)
            return false;

        var location = args.ClickLocation;
        if (!location.IsValid(EntityManager))
            return false;

        var gridUid = _transform.GetGrid(location) ?? _transform.GetGrid(args.User);
        if (!TryComp<MapGridComponent>(gridUid, out var mapGrid))
            return false;

        var tile = _mapSystem.TileIndicesFor(gridUid.Value, mapGrid, location);
        var anchored = _mapSystem.GetAnchoredEntitiesEnumerator(gridUid.Value, mapGrid, tile);

        while (anchored.MoveNext(out var ent))
        {
            if (HasComp<FSNoRCDBuildComponent>(ent.Value))
                return true;
        }

        return false;
    }
}
