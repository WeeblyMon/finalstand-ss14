using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class FSHitscanEffectSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HitscanAmmoComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnHitscanHit(EntityUid uid, HitscanAmmoComponent _, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;
        if (!HasComp<FSWeaponUpgradeStateComponent>(args.Data.Gun))
            return;
        if (!TryComp<HitscanBasicDamageComponent>(uid, out var dmgComp))
            return;

        var damage = dmgComp.Damage * _damageable.UniversalHitscanDamageModifier;
        RaiseLocalEvent(new FSProjectileHitEffectEvent
        {
            Target  = args.Data.HitEntity.Value,
            Weapon  = args.Data.Gun,
            Shooter = args.Data.Shooter,
            Damage  = damage,
        });
    }
}
