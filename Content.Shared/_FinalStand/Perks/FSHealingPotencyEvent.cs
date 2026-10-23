namespace Content.Shared._FinalStand.Perks;

// Raised on whoever applies a heal; handlers scale how much it restores. Target is the patient, when there is one.
[ByRefEvent]
public record struct FSHealingPotencyEvent(float Multiplier, EntityUid? Target = null);
