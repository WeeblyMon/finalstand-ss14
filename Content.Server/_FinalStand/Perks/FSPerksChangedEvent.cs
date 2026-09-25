namespace Content.Server._FinalStand.Perks;

/// <summary>
/// Raised on a player's body when their perks are loaded or their slotted perks change.
/// </summary>
[ByRefEvent]
public readonly record struct FSPerksChangedEvent(EntityUid MindId, FSPerkLevelsComponent Perks);
