namespace Content.Server._FinalStand.Leveling;

[RegisterComponent]
[Access(typeof(FSSpeedBonusSystem))]
public sealed partial class FSSpeedBonusComponent : Component
{
    public float SpeedMultiplier = 1f;

    // Highest level bought per upgrade, so rebuying the weapon can't stack the bonus past max level.
    public readonly Dictionary<string, int> UpgradeLevels = new();
}
