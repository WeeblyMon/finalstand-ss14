using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Upgrades.Effects;

[Serializable, NetSerializable]
public sealed class FSWarTornStateEvent : EntityEventArgs
{
    public readonly int Stacks;
    public readonly int MaxStacks;
    public readonly int BonusPct; // whole-number percent, e.g. 30 for +30%

    public FSWarTornStateEvent(int stacks, int maxStacks, int bonusPct)
    {
        Stacks = stacks; MaxStacks = maxStacks; BonusPct = bonusPct;
    }
}
