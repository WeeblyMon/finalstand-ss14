namespace Content.Shared._FinalStand.Upgrades.Effects;

// placed on suppressed targets; no-refresh — only re-applied once expired
[RegisterComponent]
public sealed partial class FSSuppressionComponent : Component
{
    public TimeSpan EndTime;
    public float SlowFactor = 0.85f;
}
