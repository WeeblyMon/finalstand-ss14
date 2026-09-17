namespace Content.Server._FinalStand.GameTicking.Rules;

// The one wave-enemy death seam. WaveGameRuleSystem owns the directed
// WaveSpawnedTagComponent + MobStateChangedEvent subscription, so anything else that needs enemy
// deaths listens here instead of taking an unfiltered broadcast on every mob in the game.
[ByRefEvent]
public readonly record struct FSWaveEnemyDiedEvent(EntityUid Enemy, EntityUid? Killer);
