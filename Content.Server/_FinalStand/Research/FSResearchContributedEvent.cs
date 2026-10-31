namespace Content.Server._FinalStand.Research;

// Raised on a mind that earned research points for the station.
[ByRefEvent]
public readonly record struct FSResearchContributedEvent(int Amount);
