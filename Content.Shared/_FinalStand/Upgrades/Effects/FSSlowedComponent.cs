namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent]
public sealed partial class FSSlowedComponent : Component
{
    [DataField] public TimeSpan EndTime;
    [DataField] public float SlowFactor = 0.7f;
}
