using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.ReadyCheck;

[Serializable, NetSerializable]
public enum ReadyStatus : byte
{
    NoResponse,
    Ready,
    NotReady,
}

// maps job prototype ID → display code shown in the CCC and PDA UI
public static class ReadyCheckDepts
{
    public static readonly Dictionary<string, string> HeadJobToDisplay = new()
    {
        { "HeadOfSecurity",    "TAC" },
        { "ResearchDirector",  "SCI" },
        { "ChiefEngineer",     "ENG" },
        { "ChiefMedicalOfficer", "MED" },
        { "Quartermaster",     "CGO" },
        { "HeadOfPersonnel",   "SRV" },
    };

    public static readonly HashSet<string> AllDisplayCodes = new(HeadJobToDisplay.Values);

    public static bool IsCaptain(string jobId) => jobId == "Captain";

    public static bool IsHeadJob(string jobId) => HeadJobToDisplay.ContainsKey(jobId);

    public static bool IsCommandJob(string jobId) => IsCaptain(jobId) || IsHeadJob(jobId);
}

// broadcast — raised at the start of every prep phase
public readonly record struct WavePrepStartedEvent;

// broadcast — raised at the start of every combat phase
public readonly record struct WaveCombatStartedEvent;

// broadcast — CCC start-wave button pressed; WaveGameRuleSystem calls StartCombatPhase on receipt
public readonly record struct WaveStartRequestEvent;

// broadcast — any dept status changed, triggers UI refresh
public readonly record struct ReadyCheckUpdatedEvent;
