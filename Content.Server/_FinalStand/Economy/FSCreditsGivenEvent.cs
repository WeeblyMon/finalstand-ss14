namespace Content.Server._FinalStand.Economy;

// Raised on a mind whenever its wallet is credited.
[ByRefEvent]
public readonly record struct FSCreditsGivenEvent(int Amount);
