namespace Content.Shared._FinalStand.Augments;

/// Raised broadcast when a wave enemy is killed. Killer is the entity that landed the killing blow.
[ByRefEvent]
public readonly record struct FSEnemyKilledEvent(EntityUid Killer);

/// Raised broadcast when a player-controlled entity dies.
[ByRefEvent]
public readonly record struct FSPlayerDiedEvent(EntityUid PlayerBody, EntityUid MindId);
