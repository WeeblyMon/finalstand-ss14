namespace Content.Server._FinalStand.GameTicking.Rules;

// Broadcast at the start of every prep phase.
[ByRefEvent]
public readonly record struct WavePrepStartedEvent;

// Broadcast at the start of every combat phase.
[ByRefEvent]
public readonly record struct WaveCombatStartedEvent;

// Broadcast when the crew starts a wave before the prep timer ran out.
[ByRefEvent]
public readonly record struct WavePrepSkippedEvent(TimeSpan Skipped);
