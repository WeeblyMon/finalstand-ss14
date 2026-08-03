using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Research;

// Sent by a console when a player picks an FSTechNodePrototype as their new research target (shared pick for RD/Captain, personal pick otherwise).
[Serializable, NetSerializable]
public sealed class FSSelectResearchNodeMessage(string nodeId) : BoundUserInterfaceMessage
{
    public readonly string NodeId = nodeId;
}

// Sent by a console when a player backs out of their own personal research pick.
[Serializable, NetSerializable]
public sealed class FSClearPersonalResearchMessage : BoundUserInterfaceMessage;

// Broadcast whenever the station-wide UnlockedNodes set changes.
[Serializable, NetSerializable]
public sealed class FSResearchUnlocksChangedEvent(HashSet<string> unlockedNodes) : EntityEventArgs
{
    public readonly HashSet<string> UnlockedNodes = unlockedNodes;
}

// Sent to a player whose FSSelectResearchNodeMessage was rejected for lacking authority.
[Serializable, NetSerializable]
public sealed class FSResearchAuthorityDeniedEvent(string reason) : EntityEventArgs
{
    public readonly string Reason = reason;
}

// Broadcast whenever the station-wide banked RP changes.
[Serializable, NetSerializable]
public sealed class FSStationRpChangedEvent(int points) : EntityEventArgs
{
    public readonly int Points = points;
}

// Sent to a single player whenever their own personal research pick or its progress changes.
[Serializable, NetSerializable]
public sealed class FSPersonalResearchStateEvent(ProtoId<FSTechNodePrototype>? nodeId, int progress) : EntityEventArgs
{
    public readonly ProtoId<FSTechNodePrototype>? NodeId = nodeId;
    public readonly int Progress = progress;
}
