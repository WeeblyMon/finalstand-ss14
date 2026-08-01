namespace Content.Server._FinalStand.Research;

// Server-local counterpart to the network-only FSResearchUnlocksChangedEvent.
public sealed class FSResearchNodeCompletedEvent(string nodeId) : EntityEventArgs
{
    public readonly string NodeId = nodeId;
}
