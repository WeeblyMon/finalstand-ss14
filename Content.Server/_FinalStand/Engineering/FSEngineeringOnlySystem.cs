using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Engineering;

// Department check for engineering-locked shop items, mirrors FSScienceOnlySystem.
public sealed partial class FSEngineeringOnlySystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedRoleSystem _roles = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private AccessReaderSystem _accessReader = default!;

    private static readonly ProtoId<DepartmentPrototype> EngineeringDept = "Engineering";

    private static readonly ProtoId<AccessLevelPrototype>[] EngineeringAccess =
        ["Engineering", "ChiefEngineer"];

    public bool IsEngineering(EntityUid user)
    {
        return HasEngineeringAccess(user) || HasEngineeringJob(user);
    }

    private bool HasEngineeringAccess(EntityUid user)
    {
        var tags = _accessReader.FindAccessTags(user);
        foreach (var access in EngineeringAccess)
        {
            if (tags.Contains(access))
                return true;
        }

        return false;
    }

    private bool HasEngineeringJob(EntityUid user)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return false;
        if (!_roles.MindHasRole(mindId, typeof(JobRoleComponent), out var jobRole))
            return false;
        var jobProtoId = jobRole.Value.Comp.JobPrototype;
        if (jobProtoId == null)
            return false;
        if (!_proto.TryIndex(EngineeringDept, out var engDept))
            return false;
        return engDept.Roles.Contains(jobProtoId.Value);
    }
}
