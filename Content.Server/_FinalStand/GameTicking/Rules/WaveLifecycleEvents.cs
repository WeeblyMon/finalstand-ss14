namespace Content.Server._FinalStand.GameTicking.Rules;

// Broadcast at the start of every prep phase.
[ByRefEvent]
public readonly record struct WavePrepStartedEvent;

// Broadcast at the start of every combat phase.
[ByRefEvent]
public readonly record struct WaveCombatStartedEvent;
