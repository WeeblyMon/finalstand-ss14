namespace Content.Shared._FinalStand.MedicalOps;

[ByRefEvent]
public readonly record struct FSTopicalAppliedEvent(EntityUid User, EntityUid Target, EntityUid Used, bool StoppedBleeding);
