using Content.Shared.Damage;

namespace Content.Shared._FinalStand.Weapons;

[ByRefEvent]
public record struct FSHitscanDamageModifyEvent(DamageSpecifier Damage);
