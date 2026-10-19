namespace Content.Server._FinalStand.Research;

// Server-local counterpart to the network-only FSResearchUnlocksChangedEvent.
[ByRefEvent]
public readonly record struct FSResearchNodeCompletedEvent(string NodeId, bool Earned = true);
