using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Sprint;

[Serializable, NetSerializable]
public sealed class FSSprintStartMessage : EntityEventArgs { }

[Serializable, NetSerializable]
public sealed class FSSprintStopMessage : EntityEventArgs { }
