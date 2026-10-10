namespace Content.Shared._FinalStand.Weapons.Prediction;

[ByRefEvent]
public readonly record struct FSProjectileLaunchedEvent(EntityUid? Gun, EntityUid? Shooter);
