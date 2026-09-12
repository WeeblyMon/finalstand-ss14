using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSMedicalRolesSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _accessReader = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPrototypeManager _prototype = default!;

    private static readonly ProtoId<DepartmentPrototype> MedicalDepartment = "Medical";
    private static readonly ProtoId<AccessLevelPrototype> ChiefMedicalOfficerAccess = "ChiefMedicalOfficer";
    private const string ChemistJob = "Chemist";

    public bool IsMedicalStaff(EntityUid mob)
    {
        return _mind.TryGetMind(mob, out var mindId, out _)
               && _jobs.MindTryGetJob(mindId, out var job)
               && _jobs.TryGetPrimaryDepartment(job.ID, out var department)
               && department.ID == MedicalDepartment;
    }

    public bool IsMedicalJob(string? jobId)
    {
        return jobId != null
               && _prototype.TryIndex(MedicalDepartment, out var department)
               && department.Roles.Contains(jobId);
    }

    public bool IsChemist(EntityUid mob)
    {
        return _mind.TryGetMind(mob, out var mindId, out _)
               && _jobs.MindTryGetJob(mindId, out var job)
               && job.ID == ChemistJob;
    }

    public bool IsCmo(EntityUid user)
    {
        return _accessReader.FindAccessTags(user).Contains(ChiefMedicalOfficerAccess);
    }
}
