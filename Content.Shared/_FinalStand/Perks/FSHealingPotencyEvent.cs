namespace Content.Shared._FinalStand.Perks;

// Raised on whoever applies a heal; handlers scale how much it restores.
[ByRefEvent]
public record struct FSHealingPotencyEvent(float Multiplier);
