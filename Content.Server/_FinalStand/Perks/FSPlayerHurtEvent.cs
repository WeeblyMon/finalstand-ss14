namespace Content.Server._FinalStand.Perks;

// Broadcast when a player takes damage that perks didn't fully block.
[ByRefEvent]
public readonly record struct FSPlayerHurtEvent(EntityUid Player);
