namespace Content.Shared._FinalStand.GameTicking;

// broadcast — raised at the start of every prep phase
public readonly record struct WavePrepStartedEvent;

// broadcast — raised at the start of every combat phase
public readonly record struct WaveCombatStartedEvent;

// broadcast — CCC start-wave button pressed; WaveGameRuleSystem calls StartCombatPhase on receipt
public readonly record struct WaveStartRequestEvent;
