using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.GameTicking;

// server -> client: current wave for the lobby. Wave 0 = no wave rule running.
[Serializable, NetSerializable]
public sealed class FSWaveStatusEvent : EntityEventArgs
{
    public readonly int Wave;
    public readonly WavePhase Phase;

    public FSWaveStatusEvent(int wave, WavePhase phase)
    {
        Wave = wave;
        Phase = phase;
    }
}

// client -> server: ask for the current wave (sent on lobby entry)
[Serializable, NetSerializable]
public sealed class FSWaveStatusRequestEvent : EntityEventArgs { }
