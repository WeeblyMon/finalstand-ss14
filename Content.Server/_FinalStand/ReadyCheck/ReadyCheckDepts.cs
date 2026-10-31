using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.ReadyCheck;

public static class ReadyCheckDepts
{
    private static readonly ProtoId<JobPrototype> Captain = "Captain";

    private static readonly HashSet<ProtoId<JobPrototype>> HeadJobs =
    [
        "HeadOfSecurity",
        "ResearchDirector",
        "ChiefEngineer",
        "ChiefMedicalOfficer",
        "Quartermaster",
        "HeadOfPersonnel",
    ];

    public static bool IsCaptain(ProtoId<JobPrototype> job) => job == Captain;

    public static bool IsHeadJob(ProtoId<JobPrototype> job) => HeadJobs.Contains(job);

    public static bool IsCommandJob(ProtoId<JobPrototype> job) => IsCaptain(job) || IsHeadJob(job);
}

[ByRefEvent]
public readonly record struct ReadyCheckUpdatedEvent;
