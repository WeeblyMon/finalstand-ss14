namespace Content.Server._FinalStand.GameTicking.Rules;


/// raised broadcast on the server when a wave's combat phase ends (all enemies dead or fallback timer
/// expired). NOT raised when an admin force-ends a wave via forcenextwave.

[ByRefEvent]
public readonly record struct WaveEndedEvent(int WaveNumber);
