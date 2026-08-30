using Content.Shared._FinalStand.GameTicking;
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
    public int EstimatedEnemyCount;
    public string FactionDisplay;
    public bool IsBossWave;
    public string WaveModifier;
    public WavePhase CurrentPhase;
    public float SecondsToPhaseEnd;
    public int AliveEnemyCount;
    public string ActiveSpawnerDirections;
    public int ReadiedPlayerCount;
    public int TotalPlayerCount;
    public List<string> NextWaveEnemyTypes;
    public int CCCCurrentDamage;
    public int CCCMaxHealth;

    public bool IsDarkWave;

    public CCCBoundUserInterfaceState(
        int waveNumber,
        int estimatedEnemyCount,
        string factionDisplay,
        bool isBossWave,
        string waveModifier,
        WavePhase currentPhase,
        float secondsToPhaseEnd,
        int aliveEnemyCount,
        string activeSpawnerDirections,
        int readiedPlayerCount,
        int totalPlayerCount,
        List<string> nextWaveEnemyTypes,
        int cccCurrentDamage = 0,
        int cccMaxHealth = 0,
        bool isDarkWave = false)
    {
        WaveNumber = waveNumber;
        EstimatedEnemyCount = estimatedEnemyCount;
        FactionDisplay = factionDisplay;
        IsBossWave = isBossWave;
        WaveModifier = waveModifier;
        CurrentPhase = currentPhase;
        SecondsToPhaseEnd = secondsToPhaseEnd;
        AliveEnemyCount = aliveEnemyCount;
        ActiveSpawnerDirections = activeSpawnerDirections;
        ReadiedPlayerCount = readiedPlayerCount;
        TotalPlayerCount = totalPlayerCount;
        NextWaveEnemyTypes = nextWaveEnemyTypes;
        CCCCurrentDamage = cccCurrentDamage;
        CCCMaxHealth = cccMaxHealth;
        IsDarkWave = isDarkWave;
    }
}

[Serializable, NetSerializable]
public sealed class CCCStartWaveMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class CCCCanStartWaveEvent : EntityEventArgs
{
    public readonly bool CanStartWave;
    public CCCCanStartWaveEvent(bool canStartWave) => CanStartWave = canStartWave;
}

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
