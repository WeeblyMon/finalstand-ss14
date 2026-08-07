namespace Content.Server._FinalStand.Economy;

/// <summary>
/// Broadcast after an admin wipe clears the prestige table. Each system that keeps persistent
/// player state resets its own components — the wallet does not reach into leveling or perks.
/// </summary>
[ByRefEvent]
public readonly record struct FSPrestigeWipedEvent;
