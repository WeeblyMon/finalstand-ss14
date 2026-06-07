namespace Content.Server._FinalStand.Upgrades;

[RegisterComponent]
public sealed partial class FSAftershockTrackerComponent : Component
{
    public EntityUid? Weapon;
    public EntityUid? Shooter;
}
