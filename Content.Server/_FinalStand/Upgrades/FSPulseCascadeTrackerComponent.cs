using Content.Shared.Damage;

namespace Content.Server._FinalStand.Upgrades;

[RegisterComponent]
public sealed partial class FSPulseCascadeTrackerComponent : Component
{
    public EntityUid? Weapon;
    public EntityUid? Shooter;
    public DamageSpecifier Damage = new();
}
