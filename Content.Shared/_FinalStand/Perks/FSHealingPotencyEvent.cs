namespace Content.Shared._FinalStand.Perks;

[ByRefEvent]
public record struct FSHealingPotencyEvent(float Multiplier, EntityUid? Target = null);
