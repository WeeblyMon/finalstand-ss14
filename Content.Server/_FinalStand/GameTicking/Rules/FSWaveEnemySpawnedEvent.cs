using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.GameTicking.Rules;

// Raised broadcast once a wave enemy is spawned and scaled.
[ByRefEvent]
public readonly record struct FSWaveEnemySpawnedEvent(EntityUid Enemy, EntProtoId Prototype);
