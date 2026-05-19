namespace Content.Shared._FinalStand.Crit;

/// raised on target after every projectile hit so damage numbers know if it was a crit
public sealed class CritLandedEvent : EntityEventArgs
{
    public EntityUid Target;
    public EntityUid Shooter;
    public float FinalDamage;
    public bool WasCrit;
}
