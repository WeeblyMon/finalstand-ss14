using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class LifeStealUpgradeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null || ev.Shooter == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || state.LifeStealPercent <= 0f)
            return;

        var totalDamage = ev.Damage.GetTotal().Float();
        if (totalDamage <= 0f)
            return;

        var healAmount = totalDamage * state.LifeStealPercent;
        // HealEvenly distributes healing across all damage types the player actually has.
        // DamageDict["Brute"] does not work at runtime — "Brute" is a group name and is only
        // expanded during YAML deserialization, not by TryChangeDamage.
        _damageable.HealEvenly(ev.Shooter.Value, FixedPoint2.New(-healAmount));
    }
}
