using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSMedicPingActionEvent : InstantActionEvent { }

// Broadcast so every client can draw the bubble, not just those already looking at the caller.
[Serializable, NetSerializable]
public sealed class FSMedicPingEvent : EntityEventArgs
{
    public NetEntity Caller;
    public bool IsHurt;

    public FSMedicPingEvent(NetEntity caller, bool isHurt)
    {
        Caller = caller;
        IsHurt = isHurt;
    }
}
