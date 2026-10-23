namespace Content.Server._FinalStand.CCC;

// Raised broadcast on every hit that adds damage to the CCC, before a lost life is handled.
[ByRefEvent]
public readonly record struct FSCccDamagedEvent(EntityUid Ccc);
