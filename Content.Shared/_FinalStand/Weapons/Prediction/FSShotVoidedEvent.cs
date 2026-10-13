using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Weapons.Prediction;

[Serializable, NetSerializable]
public sealed class FSShotVoidedEvent(GameTick tick, List<int> indices) : EntityEventArgs
{
    public readonly GameTick Tick = tick;
    public readonly List<int> Indices = indices;
}
