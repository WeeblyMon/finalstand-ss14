namespace Content.Shared._FinalStand.RCD;

// Raised on the RCD once it has spawned an object, so FS can charge materials and record the owner.
[ByRefEvent]
public readonly record struct FSRCDBuiltEvent(EntityUid User, EntityUid Built, Direction Direction);
