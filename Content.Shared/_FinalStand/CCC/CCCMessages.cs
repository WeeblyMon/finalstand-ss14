using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.ReadyCheck;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.CCC;

[Serializable, NetSerializable]
public enum CCCUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CCCBoundUserInterfaceState : BoundUserInterfaceState
{
    public int WaveNumber;
    public int EstimatedEnemyCount;     // server has already applied ±20% variance
    public string FactionDisplay;       // "Xenos detected", "Undead detected", etc.
    public bool IsBossWave;
    public string WaveModifier;
    public WavePhase CurrentPhase;
    public float SecondsToPhaseEnd;
    public int AliveEnemyCount;
    public int ActiveSpawnerCount;
    public Dictionary<string, ReadyStatus> DepartmentStatus;
    public int ReadyCount;

    public CCCBoundUserInterfaceState(
        int waveNumber,
        int estimatedEnemyCount,
        string factionDisplay,
        bool isBossWave,
        string waveModifier,
        WavePhase currentPhase,
        float secondsToPhaseEnd,
        int aliveEnemyCount,
        int activeSpawnerCount,
        Dictionary<string, ReadyStatus> departmentStatus,
        int readyCount)
    {
        WaveNumber = waveNumber;
        EstimatedEnemyCount = estimatedEnemyCount;
        FactionDisplay = factionDisplay;
        IsBossWave = isBossWave;
        WaveModifier = waveModifier;
        CurrentPhase = currentPhase;
        SecondsToPhaseEnd = secondsToPhaseEnd;
        AliveEnemyCount = aliveEnemyCount;
        ActiveSpawnerCount = activeSpawnerCount;
        DepartmentStatus = departmentStatus;
        ReadyCount = readyCount;
    }
}

[Serializable, NetSerializable]
public sealed class CCCStartWaveMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class CCCBroadcastMessage : BoundUserInterfaceMessage
{
    public string Text;

    public CCCBroadcastMessage(string text)
    {
        Text = text;
    }
}
