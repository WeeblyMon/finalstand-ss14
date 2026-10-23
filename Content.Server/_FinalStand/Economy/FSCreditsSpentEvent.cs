namespace Content.Server._FinalStand.Economy;

// Raised whenever a player pays for something. Transfers to another player don't count.
[ByRefEvent]
public readonly record struct FSCreditsSpentEvent(EntityUid Mind, int Amount);
