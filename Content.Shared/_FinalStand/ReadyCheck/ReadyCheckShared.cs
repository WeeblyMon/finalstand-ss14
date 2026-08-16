namespace Content.Shared._FinalStand.ReadyCheck;

public static class ReadyCheckDepts
{
    private static readonly HashSet<string> HeadJobs = new()
    {
        "HeadOfSecurity",
        "ResearchDirector",
        "ChiefEngineer",
        "ChiefMedicalOfficer",
        "Quartermaster",
        "HeadOfPersonnel",
    };

    public static bool IsCaptain(string jobId) => jobId == "Captain";

    public static bool IsHeadJob(string jobId) => HeadJobs.Contains(jobId);

    public static bool IsCommandJob(string jobId) => IsCaptain(jobId) || IsHeadJob(jobId);
}

public readonly record struct ReadyCheckUpdatedEvent;
