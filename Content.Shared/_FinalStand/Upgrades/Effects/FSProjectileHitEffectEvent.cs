using Content.Shared._FinalStand.Shop;
using Content.Shared.Damage;

namespace Content.Shared._FinalStand.Upgrades.Effects;

// Broadcast on every projectile hit. Upgrade systems subscribe here instead of
// ProjectileHitEvent — Robust only allows one directed subscriber per (comp, event) pair.
// Subscribers may set AdditionalMultiplier; CritSystem applies it back to args.Damage.
public sealed class FSProjectileHitEffectEvent : EntityEventArgs
{
    public EntityUid Target;
    public EntityUid? Weapon;

    // Fetched once by the raiser. Null when the weapon has no upgrade state — the event still
    // fires, because the flintlock synergy arms plain weapons that never carry one.
    public FSWeaponUpgradeStateComponent? State;
    public EntityUid? Shooter;
    public EntityUid? ProjectileUid;
    public DamageSpecifier Damage = new();
    // Always multiply into this, never assign — several subscribers run per hit and an
    // assignment silently discards the ones that ran first.
    public float AdditionalMultiplier = 1f;
    public bool WasCrit = false;
}
