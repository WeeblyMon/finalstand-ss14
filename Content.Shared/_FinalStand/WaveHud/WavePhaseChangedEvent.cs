using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.WaveHud;

[Serializable, NetSerializable]
public sealed class WavePhaseChangedEvent : EntityEventArgs
{
    public readonly bool IsPrepPhase;
    public WavePhaseChangedEvent(bool isPrepPhase) => IsPrepPhase = isPrepPhase;
}
