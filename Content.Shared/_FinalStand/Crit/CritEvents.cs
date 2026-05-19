namespace Content.Shared._FinalStand.Crit;

/// <summary>
///     Raised as a local event on the target entity after every projectile hit (crit or normal).
///     Damage numbers subscribe to this instead of DamageChangedEvent so they can set colour correctly.
/// </summary>
public sealed class CritLandedEvent : EntityEventArgs
{
    public EntityUid Target;
    public EntityUid Shooter;
    public float FinalDamage;
    public bool WasCrit;
}
