using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Research;

// Sent by a console when a player picks an FSTechNodePrototype as the new active research target.
[Serializable, NetSerializable]
public sealed class FSSelectResearchNodeMessage(string nodeId) : BoundUserInterfaceMessage
{
    public readonly string NodeId = nodeId;
}

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
