using Content.Shared.DoAfter;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.SmartReload;

[Serializable, NetSerializable]
public sealed class FSSmartReloadMessage : EntityEventArgs
{
    public NetEntity Gun { get; init; }
}

[Serializable, NetSerializable]
public sealed class FSEjectMessage : EntityEventArgs
{
    public NetEntity Gun { get; init; }
}

[Serializable, NetSerializable]
public sealed partial class FSMagReloadDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public sealed partial class FSShellInsertDoAfterEvent : SimpleDoAfterEvent { }

[Serializable, NetSerializable]
public sealed partial class FSChamberFillDoAfterEvent : SimpleDoAfterEvent { }
