using Content.Shared._FinalStand.Shop;
using Content.Shared.Damage;

namespace Content.Shared._FinalStand.Upgrades.Effects;

// Broadcast on every projectile hit, since Robust allows only one directed subscriber per (comp, event) pair on ProjectileHitEvent.
[ByRefEvent]
public struct FSProjectileHitEffectEvent
{
    public EntityUid Target;
    public EntityUid? Weapon;

    // Null when the weapon has no upgrade state. Still fires, since the flintlock synergy arms plain weapons.
    public FSWeaponUpgradeStateComponent? State;
    public EntityUid? Shooter;
    public EntityUid? ProjectileUid;
    public DamageSpecifier Damage;
    // Always multiply into this, never assign; several subscribers run per hit.
    public float AdditionalMultiplier = 1f;
    public bool WasCrit;

    public FSProjectileHitEffectEvent(EntityUid target, DamageSpecifier damage)
    {
        Target = target;
        Damage = damage;
    }
}
