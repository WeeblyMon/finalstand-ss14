namespace Content.Server._FinalStand.Upgrades;

// placed on bleeding targets; re-applied on each hit to reset timer and update DPS
[RegisterComponent]
public sealed partial class FSBleedingComponent : Component
{
    public float DamagePerSecond;
    public TimeSpan ExpiresAt;
    public TimeSpan NextTickAt;
    public EntityUid? Instigator;
}
