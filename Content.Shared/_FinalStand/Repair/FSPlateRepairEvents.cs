using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Repair;

[Serializable, NetSerializable]
public sealed partial class FSPlateFitDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class FSPlateWeldDoAfterEvent : SimpleDoAfterEvent;
