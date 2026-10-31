using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Research;

// Sets the shared pick for RD/Captain, a personal pick for everyone else.
[Serializable, NetSerializable]
public sealed class FSSelectResearchNodeMessage(string nodeId) : BoundUserInterfaceMessage
{
    public readonly string NodeId = nodeId;
}

[Serializable, NetSerializable]
public sealed class FSEnqueueResearchNodeMessage(string nodeId) : BoundUserInterfaceMessage
{
    public readonly string NodeId = nodeId;
}

[Serializable, NetSerializable]
public sealed class FSDequeueResearchNodeMessage(string nodeId) : BoundUserInterfaceMessage
{
    public readonly string NodeId = nodeId;
}

[Serializable, NetSerializable]
public sealed class FSClearPersonalResearchMessage : BoundUserInterfaceMessage;

// Back to banking RP with nothing selected.
[Serializable, NetSerializable]
public sealed class FSClearSharedResearchMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class FSResearchUnlocksChangedEvent(HashSet<string> unlockedNodes) : EntityEventArgs
{
    public readonly HashSet<string> UnlockedNodes = unlockedNodes;
}

[Serializable, NetSerializable]
public sealed class FSResearchAuthorityDeniedEvent(string reason) : EntityEventArgs
{
    public readonly string Reason = reason;
}

[Serializable, NetSerializable]
public sealed class FSStationRpChangedEvent(int points) : EntityEventArgs
{
    public readonly int Points = points;
}

// Sent only to the player it describes.
[Serializable, NetSerializable]
public sealed class FSPersonalResearchStateEvent(ProtoId<FSTechNodePrototype>? nodeId, int progress, List<string> queue) : EntityEventArgs
{
    public readonly ProtoId<FSTechNodePrototype>? NodeId = nodeId;
    public readonly int Progress = progress;
    public readonly List<string> Queue = queue;
}

[Serializable, NetSerializable]
public sealed class FSPlayerResearchAuthorityEvent(bool isRdOrCaptain) : EntityEventArgs
{
    public readonly bool IsRdOrCaptain = isRdOrCaptain;
}

[Serializable, NetSerializable]
public sealed class FSSharedResearchStateEvent(ProtoId<FSTechNodePrototype>? nodeId, int progress) : EntityEventArgs
{
    public readonly ProtoId<FSTechNodePrototype>? NodeId = nodeId;
    public readonly int Progress = progress;
}
