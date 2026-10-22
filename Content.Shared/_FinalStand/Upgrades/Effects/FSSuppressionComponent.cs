namespace Content.Shared._FinalStand.Upgrades.Effects;

// placed on suppressed targets; not refreshed, only re-applied once expired
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FSSuppressionComponent : Component
{
    [AutoPausedField] public TimeSpan EndTime;
    public float SlowFactor = 0.85f;
}
