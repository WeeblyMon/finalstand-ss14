using Content.Shared._FinalStand.GameTicking;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.CCC;

[Serializable, NetSerializable]
public enum CCCUiKey : byte
{
    Key,
}

// Phase, timer, alive count and dark wave come from FSWaveStateComponent; this carries the rest,
// and is only sent when it changes.
[Serializable, NetSerializable]
public sealed class CCCBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly int WaveNumber;
    public readonly int EstimatedEnemyCount;
    public readonly string FactionDisplay;
    public readonly bool IsBossWave;
    public readonly string ActiveSpawnerDirections;
    public readonly int ReadiedPlayerCount;
    public readonly int TotalPlayerCount;
    public readonly List<string> NextWaveEnemyTypes;
    public readonly int CCCCurrentDamage;
    public readonly int CCCMaxHealth;

    public CCCBoundUserInterfaceState(
        int waveNumber,
        int estimatedEnemyCount,
        string factionDisplay,
        bool isBossWave,
        string activeSpawnerDirections,
        int readiedPlayerCount,
        int totalPlayerCount,
        List<string> nextWaveEnemyTypes,
        int cccCurrentDamage,
        int cccMaxHealth)
    {
        WaveNumber = waveNumber;
        EstimatedEnemyCount = estimatedEnemyCount;
        FactionDisplay = factionDisplay;
        IsBossWave = isBossWave;
        ActiveSpawnerDirections = activeSpawnerDirections;
        ReadiedPlayerCount = readiedPlayerCount;
        TotalPlayerCount = totalPlayerCount;
        NextWaveEnemyTypes = nextWaveEnemyTypes;
        CCCCurrentDamage = cccCurrentDamage;
        CCCMaxHealth = cccMaxHealth;
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
