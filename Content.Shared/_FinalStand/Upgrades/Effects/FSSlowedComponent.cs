namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent]
public sealed partial class FSSlowedComponent : Component
{
    public readonly Dictionary<string, (float Factor, TimeSpan EndTime)> Slows = new();

    public float Applied = 1f;
}
