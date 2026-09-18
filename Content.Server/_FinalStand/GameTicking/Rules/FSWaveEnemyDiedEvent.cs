namespace Content.Server._FinalStand.GameTicking.Rules;

[ByRefEvent]
public readonly record struct FSWaveEnemyDiedEvent(EntityUid Enemy, EntityUid? Killer);
