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
    public List<string> NextWaveEnemyTypes;
    public int CCCCurrentDamage;
    public int CCCMaxHealth;

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
        int readyCount,
        List<string> nextWaveEnemyTypes,
        int cccCurrentDamage = 0,
        int cccMaxHealth = 2000)
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
        NextWaveEnemyTypes = nextWaveEnemyTypes;
        CCCCurrentDamage = cccCurrentDamage;
        CCCMaxHealth = cccMaxHealth;
    }
}

[Serializable, NetSerializable]
public sealed class CCCStartWaveMessage : BoundUserInterfaceMessage { }

// Sent server→client to tell the opening player whether they can start the wave.
// Avoids unreliable client-side mind/job lookups (JobComponent is server-only).
[Serializable, NetSerializable]
public sealed class CCCCanStartWaveEvent : EntityEventArgs
{
    public readonly bool CanStartWave;
    public CCCCanStartWaveEvent(bool canStartWave) => CanStartWave = canStartWave;
}

// Broadcast server→all-clients when the CCC takes damage. Client-side 3s timeout controls hide.
[Serializable, NetSerializable]
public sealed class CCCUnderAttackEvent : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed class CCCBroadcastMessage : BoundUserInterfaceMessage
{
    public string Text;

    public CCCBroadcastMessage(string text)
    {
        Text = text;
    }
}
