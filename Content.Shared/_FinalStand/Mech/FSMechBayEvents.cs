using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Mech;

[Serializable, NetSerializable]
public sealed partial class FSMechBayStageDoAfterEvent : SimpleDoAfterEvent;

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
