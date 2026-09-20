namespace Content.Shared._FinalStand.MedicalOps;

[ByRefEvent]
public readonly record struct FSSurgeryCompletedEvent(EntityUid Surgeon, EntityUid Patient, EntityUid Part, EntityUid Surgery);
