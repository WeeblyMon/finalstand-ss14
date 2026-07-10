using Content.Shared.Damage;

namespace Content.Shared._FinalStand.Upgrades.Effects;

// Broadcast on every projectile hit. Upgrade systems subscribe here instead of
// ProjectileHitEvent — Robust only allows one directed subscriber per (comp, event) pair.
// Subscribers may set AdditionalMultiplier; CritSystem applies it back to args.Damage.
public sealed class FSProjectileHitEffectEvent : EntityEventArgs
{
    public EntityUid Target;
    public EntityUid? Weapon;
    public EntityUid? Shooter;
    public EntityUid? ProjectileUid;
    public DamageSpecifier Damage = new();
    public float AdditionalMultiplier = 1f;
}
