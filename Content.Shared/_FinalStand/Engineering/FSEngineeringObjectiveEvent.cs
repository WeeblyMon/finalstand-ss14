using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Engineering;

[Serializable, NetSerializable]
public enum FSObjectiveKind : byte
{
    Bay,
    BayReady,
    Silo,
    Emplacement,
}

[Serializable, NetSerializable]
public sealed class FSEngineeringObjectiveEvent(List<string> lines) : EntityEventArgs
{
    public readonly List<string> Lines = lines;

    public List<FSObjectiveKind> Kinds = new();

    // Per line: Mech Bay stage and total, and whether the next part is Science's.
    public List<int> Stages = new();
    public List<int> Totals = new();
    public List<bool> Science = new();
}
