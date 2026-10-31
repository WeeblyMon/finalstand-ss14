namespace Content.Shared._FinalStand.Crit;

// Raised on the target after a hit lands, so damage numbers know whether it was a crit.
[ByRefEvent]
public readonly record struct CritLandedEvent(EntityUid Target, EntityUid Shooter, float FinalDamage, bool WasCrit);
