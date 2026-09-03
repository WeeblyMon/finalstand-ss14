namespace Content.Shared._FinalStand.Deployables;

// Raised on the freshly planted entity so each deployable can carry its upgrades over from the item.
[ByRefEvent]
public readonly record struct FSDeployableDeployedEvent(EntityUid Item, EntityUid User);
