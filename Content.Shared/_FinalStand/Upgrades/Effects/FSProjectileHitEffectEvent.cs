using Content.Shared._FinalStand.Shop;
using Content.Shared.Damage;

namespace Content.Shared._FinalStand.Upgrades.Effects;

// Broadcast on every projectile hit, since Robust allows only one directed subscriber per (comp, event) pair on ProjectileHitEvent.
public sealed class FSProjectileHitEffectEvent : EntityEventArgs
{
    public EntityUid Target;
    public EntityUid? Weapon;

    // Null when the weapon has no upgrade state — still fires, since the flintlock synergy arms plain weapons.
    public FSWeaponUpgradeStateComponent? State;
    public EntityUid? Shooter;
    public EntityUid? ProjectileUid;
    public DamageSpecifier Damage = new();
    // Always multiply into this, never assign — several subscribers run per hit.
    public float AdditionalMultiplier = 1f;
    public bool WasCrit = false;
}
