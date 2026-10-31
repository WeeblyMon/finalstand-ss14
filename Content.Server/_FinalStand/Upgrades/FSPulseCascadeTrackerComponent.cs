using Content.Shared.Damage;

namespace Content.Server._FinalStand.Upgrades;

[RegisterComponent]
[Access(typeof(Content.Server._FinalStand.Upgrades.Effects.PulseCascadeUpgradeSystem))]
public sealed partial class FSPulseCascadeTrackerComponent : Component
{
    public EntityUid? Weapon;
    public EntityUid? Shooter;
    public DamageSpecifier Damage = new();
}
