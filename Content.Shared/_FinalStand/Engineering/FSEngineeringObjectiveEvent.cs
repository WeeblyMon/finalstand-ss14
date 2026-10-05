using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Engineering;

[Serializable, NetSerializable]
public sealed class FSEngineeringObjectiveEvent(List<string> lines) : EntityEventArgs
{
    public readonly List<string> Lines = lines;
}
