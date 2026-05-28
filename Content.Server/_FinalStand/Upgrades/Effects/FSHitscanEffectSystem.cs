using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Crit;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Server._FinalStand.Upgrades.Effects;

// raises CritLandedEvent with the correct shooter so hitscan weapons show enemy health bars
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
        if (!TryComp<HitscanBasicDamageComponent>(uid, out var dmgComp))
            return;

        var damage = dmgComp.Damage * _damageable.UniversalHitscanDamageModifier;

        if (HasComp<WaveSpawnedTagComponent>(args.Data.HitEntity.Value) && args.Data.Shooter is { } shooter)
        {
            RaiseLocalEvent(args.Data.HitEntity.Value, new CritLandedEvent
            {
                Target      = args.Data.HitEntity.Value,
                Shooter     = shooter,
                FinalDamage = damage.GetTotal().Float(),
                WasCrit     = false,
            });
        }

        if (!HasComp<FSWeaponUpgradeStateComponent>(args.Data.Gun))
            return;

        RaiseLocalEvent(new FSProjectileHitEffectEvent
        {
            Target  = args.Data.HitEntity.Value,
            Weapon  = args.Data.Gun,
            Shooter = args.Data.Shooter,
            Damage  = damage,
        });
    }
}
