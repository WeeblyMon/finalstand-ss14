using Content.Shared._FinalStand.FriendlyFire;
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
        if (ev.State is not { } state || state.LifeStealPercent <= 0f)
            return;

        if (HasComp<FSPlayerDamageImmuneComponent>(ev.Target) || HasComp<FSFriendlyFireComponent>(ev.Target))
            return;

        var totalDamage = ev.Damage.GetTotal().Float();
        if (totalDamage <= 0f)
            return;

        var healAmount = totalDamage * state.LifeStealPercent;
        _damageable.HealEvenly(ev.Shooter.Value, FixedPoint2.New(-healAmount));
    }
}
