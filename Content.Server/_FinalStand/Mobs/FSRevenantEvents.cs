namespace Content.Server._FinalStand.Mobs;

[ByRefEvent]
public readonly record struct FSRevenantExecutedEvent(EntityUid Revenant, EntityUid Victim);
