using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.GameTicking;

[Serializable, NetSerializable]
public enum WavePhase : byte
{
    Prep,
    Combat,
}
