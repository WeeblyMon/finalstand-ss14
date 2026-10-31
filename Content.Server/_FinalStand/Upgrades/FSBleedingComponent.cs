namespace Content.Server._FinalStand.Upgrades;

// placed on bleeding targets; re-applied on each hit to reset timer and update DPS
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FSBleedingComponent : Component
{
    public float DamagePerSecond;
    [AutoPausedField] public TimeSpan ExpiresAt;
    [AutoPausedField] public TimeSpan NextTickAt;
    public EntityUid? Instigator;
}
