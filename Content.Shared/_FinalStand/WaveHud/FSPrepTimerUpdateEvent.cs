using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.WaveHud;

[Serializable, NetSerializable]
public sealed class FSPrepTimerUpdateEvent : EntityEventArgs
{
    public readonly float SecondsRemaining;
    public readonly bool IsPrepPhase;

    public FSPrepTimerUpdateEvent(float secondsRemaining, bool isPrepPhase)
    {
        SecondsRemaining = secondsRemaining;
        IsPrepPhase = isPrepPhase;
    }
}
