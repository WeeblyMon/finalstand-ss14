namespace Content.Server._FinalStand.Upgrades;

[RegisterComponent]
[Access(typeof(Content.Server._FinalStand.Upgrades.Effects.AftershockUpgradeSystem))]
public sealed partial class FSAftershockTrackerComponent : Component
{
    public EntityUid? Weapon;
    public EntityUid? Shooter;
}
