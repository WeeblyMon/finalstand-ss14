using Content.Shared.Damage;

namespace Content.Shared._FinalStand.Upgrades.Effects;

// Broadcast on every projectile hit. Upgrade systems subscribe here instead of
// ProjectileHitEvent — Robust only allows one directed subscriber per (comp, event) pair.
public sealed class FSProjectileHitEffectEvent : EntityEventArgs
{
    public EntityUid Target;
    public EntityUid? Weapon;
    public EntityUid? Shooter;
    public DamageSpecifier Damage = new();
}
