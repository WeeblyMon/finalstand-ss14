using Content.Server._FinalStand.Placement;
using Content.Shared._FinalStand.Science;
using Content.Shared.Access.Systems;
using Content.Shared.Access;
using Content.Shared.GameTicking;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Science;

// Restricts FSScienceOnlyComponent-tagged items to Science department members, mirrors FSRCDEngineerOnlySystem.
public sealed partial class FSScienceOnlySystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;

    private static readonly ProtoId<DepartmentPrototype> ScienceDept = "Science";

    private static readonly ProtoId<AccessLevelPrototype>[] ScienceAccess =
        ["Research", "ResearchDirector"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSScienceOnlyComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<FSScienceOnlyComponent, UseInHandEvent>(OnUseInHand,
            before: [typeof(FSPlacementSystem)]);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnAttemptShoot(EntityUid uid, FSScienceOnlyComponent comp, ref AttemptShootEvent args)
    {
        if (args.Cancelled || IsScience(args.User))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("fs-science-only-use");
    }

    private void OnUseInHand(EntityUid uid, FSScienceOnlyComponent comp, UseInHandEvent args)
    {
        if (args.Handled || IsScience(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("fs-science-only-use"), uid, args.User);
    }

    public bool IsScience(EntityUid user)
    {
        return HasScienceAccess(user) || HasScienceJob(user);
    }

    // Promotions hand out a science ID without changing the mind's job, so access has to count too.
    private bool HasScienceAccess(EntityUid user)
    {
        var tags = _accessReader.FindAccessTags(user);
        foreach (var access in ScienceAccess)
        {
            if (tags.Contains(access))
                return true;
        }

        return false;
    }

    private bool HasScienceJob(EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return false;
        if (!_roles.MindHasRole(mindId, typeof(JobRoleComponent), out var jobRole))
            return false;
        var jobProtoId = jobRole.Value.Comp.JobPrototype;
        if (jobProtoId == null)
            return false;
        if (!_proto.TryIndex(ScienceDept, out var sciDept))
            return false;
        return sciDept.Roles.Contains(jobProtoId.Value);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        RaiseNetworkEvent(new FSPlayerScienceStatusEvent(IsScience(ev.Mob)), Filter.SinglePlayer(ev.Player));
    }
}
