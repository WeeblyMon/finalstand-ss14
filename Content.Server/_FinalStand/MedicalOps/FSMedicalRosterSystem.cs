using System.Collections.Frozen;
using Content.Server.GameTicking;
using Content.Shared.Access;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

// Sole authority for medical identity. Membership is the rule - a job counts as medical if the
// Medical department lists it - which is what lets the CMO carry a medigun. Resolution walks
// mind -> job -> department prototype, so it is cached per mob and rebuilt only when a player
// spawns, attaches or detaches.
public sealed class FSMedicalRosterSystem : EntitySystem
{
    [Dependency] private AccessReaderSystem _access = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<DepartmentPrototype> MedicalDepartment = "Medical";
    private static readonly ProtoId<AccessLevelPrototype> CmoAccess = "ChiefMedicalOfficer";

    public const string CmoJob = "ChiefMedicalOfficer";
    public const string ChemistJob = "Chemist";

    // Deliberately uncached. A cache here has to be invalidated on spawn, attach, detach, ghost and
    // round restart, and getting any one of those wrong caches "not medical" for the rest of the
    // round - which silently kills the triage feed, the ping filter and directives for that player.
    // This is a component lookup plus a prototype index, called at most once a second per session.
    public string? JobOf(EntityUid mob)
    {
        return _mind.TryGetMind(mob, out var mindId, out _) && _jobs.MindTryGetJob(mindId, out var job)
            ? job.ID
            : null;
    }

    public bool IsMedical(EntityUid mob) => IsMedicalJob(JobOf(mob));

    public bool IsMedicalJob(string? jobId) =>
        jobId != null
        && _prototypes.TryIndex(MedicalDepartment, out var department)
        && department.Roles.Contains(jobId);

    public bool IsChemist(EntityUid mob) => JobOf(mob) == ChemistJob;

    public bool IsCmoJob(EntityUid mob) => JobOf(mob) == CmoJob;

    /// <summary>Card-based gate, for anything an ID reader would guard. Distinct from the job.</summary>
    public bool HasCmoAccess(EntityUid user) => _access.FindAccessTags(user).Contains(CmoAccess);

    public IEnumerable<(ICommonSession Session, EntityUid Mob)> Medics()
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is { } mob && IsMedical(mob))
                yield return (session, mob);
        }
    }

    public Filter MedicalFilter()
    {
        var filter = Filter.Empty();
        foreach (var (session, _) in Medics())
            filter.AddPlayer(session);

        return filter;
    }
}
