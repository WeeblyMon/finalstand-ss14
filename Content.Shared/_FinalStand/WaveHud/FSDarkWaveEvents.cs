using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.WaveHud;

[Serializable, NetSerializable]
public sealed class FSDarkWaveWarningEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class FSDarkWaveStartedEvent : EntityEventArgs
{
    public float DurationSeconds;

    public FSDarkWaveStartedEvent(float durationSeconds = 0f)
    {
        DurationSeconds = durationSeconds;
    }
}

[Serializable, NetSerializable]
public sealed class FSDarkWaveEndedEvent : EntityEventArgs;
