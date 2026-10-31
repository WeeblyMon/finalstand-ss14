using Content.Shared._FinalStand.GameTicking;

namespace Content.Server._FinalStand.GameTicking.Rules;

// Broadcast when the crew clears the victory wave, before the endless vote.
[ByRefEvent]
public readonly record struct FSRoundVictoryEvent(int WaveNumber, FSDifficulty Difficulty);
