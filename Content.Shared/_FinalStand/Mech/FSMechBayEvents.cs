using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Mech;

[Serializable, NetSerializable]
public sealed partial class FSMechBayWeldDoAfterEvent : DoAfterEvent
{
    public readonly string Slot;

    public FSMechBayWeldDoAfterEvent(string slot)
    {
        Slot = slot;
    }

    public override DoAfterEvent Clone() => this;
}

[Serializable, NetSerializable]
public sealed partial class FSMechBayCraftDoAfterEvent : DoAfterEvent
{
    public readonly int Index;

    public FSMechBayCraftDoAfterEvent(int index)
    {
        Index = index;
    }

    public override DoAfterEvent Clone() => this;
}
