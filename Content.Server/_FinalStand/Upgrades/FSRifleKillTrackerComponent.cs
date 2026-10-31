using Content.Shared.Damage;

namespace Content.Server._FinalStand.Upgrades;

// placed on wave enemies on hit by BattleTrance; detects kills to accumulate damage stacks
[RegisterComponent]
[Access(typeof(Content.Server._FinalStand.Upgrades.Effects.BattleTranceUpgradeSystem))]
public sealed partial class FSRifleKillTrackerComponent : Component
{
    public EntityUid? Weapon;
    public EntityUid? Shooter;
    public DamageSpecifier Damage = new();
}
