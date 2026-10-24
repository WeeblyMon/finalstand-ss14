namespace Content.Server._FinalStand.MedicalOps;

// Raised broadcast when a player brings someone else back from dead, before the medic payout cap is checked.
[ByRefEvent]
public readonly record struct FSAllyRevivedEvent(EntityUid HealerMind, EntityUid Patient);
