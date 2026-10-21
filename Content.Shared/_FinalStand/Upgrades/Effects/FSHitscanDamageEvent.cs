using Content.Shared.Damage;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Shared._FinalStand.Upgrades.Effects;

// Raised on a hitscan before HitscanBasicDamageSystem deals its damage, so upgrades, crits and hit effects can
// scale the shot the way they scale a projectile's damage.
[ByRefEvent]
public struct FSHitscanDamageEvent(HitscanRaycastFiredData data, DamageSpecifier damage)
{
    public readonly HitscanRaycastFiredData Data = data;
    public DamageSpecifier Damage = damage;
}
