namespace Content.Server._FinalStand.MedicalOps;

/// <summary>
/// Broadcast when a player heals someone other than themselves, including through chems they injected.
/// </summary>
[ByRefEvent]
public readonly record struct FSAllyHealedEvent(EntityUid HealerMind, EntityUid Patient, float Amount);
