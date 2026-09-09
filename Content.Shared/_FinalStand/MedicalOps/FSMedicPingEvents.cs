using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSMedicPingActionEvent : InstantActionEvent { }

public sealed partial class FSChemRequestActionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public enum FSPingKind : byte
{
    Medic,
    Chem,
}

[Serializable, NetSerializable]
public sealed class FSMedicPingEvent : EntityEventArgs
{
    public NetEntity Caller;
    public bool IsHurt;
    public FSPingKind Kind;

    public FSMedicPingEvent(NetEntity caller, bool isHurt, FSPingKind kind = FSPingKind.Medic)
    {
        Caller = caller;
        IsHurt = isHurt;
        Kind = kind;
    }
}
