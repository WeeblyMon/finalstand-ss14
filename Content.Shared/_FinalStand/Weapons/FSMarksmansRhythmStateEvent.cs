using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Weapons;

[Serializable, NetSerializable]
public sealed class FSMarksmansRhythmStateEvent : EntityEventArgs
{
    public readonly int Stacks;
    public readonly int MaxStacks;
    public readonly int BonusPct;

    public FSMarksmansRhythmStateEvent(int stacks, int maxStacks, int bonusPct)
    {
        Stacks = stacks; MaxStacks = maxStacks; BonusPct = bonusPct;
    }
}
