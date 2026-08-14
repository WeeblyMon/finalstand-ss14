using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Crit;
using Content.Shared._FinalStand.Crit;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;

namespace Content.Server._FinalStand.Upgrades.Effects;

// Bridges hitscan into the upgrade-effect pipeline. Vanilla HitscanBasicDamageSystem has already
// applied the base damage by the time effects run, and the event carries no mutable damage, so a
// crit or an AdditionalMultiplier is paid as a second TryChangeDamage for the difference.
public sealed partial class FSHitscanEffectSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private CritSystem _crit = default!;

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
        var target = args.Data.HitEntity.Value;

        TryComp<FSWeaponUpgradeStateComponent>(args.Data.Gun, out var upgradeState);

        // damage stays the vanilla-applied baseline for PayDelta; wantedDamage is what we actually owe.
        var wantedDamage = damage;
        if (upgradeState is { DamageMultiplier: > 1.0f })
            wantedDamage *= upgradeState.DamageMultiplier;

        var didCrit = false;
        var critMultiplier = 1f;
        if (args.Data.Shooter is { } critShooter
            && _crit.TryRollCrit(critShooter, args.Data.Gun, target, out critMultiplier))
        {
            didCrit = true;
            _crit.MarkPendingCrit(critShooter, target);
        }

        var finalDamage = didCrit ? wantedDamage * critMultiplier : wantedDamage;

        if (HasComp<WaveSpawnedTagComponent>(target) && args.Data.Shooter is { } shooter)
        {
            RaiseLocalEvent(target, new CritLandedEvent
            {
                Target      = target,
                Shooter     = shooter,
                FinalDamage = finalDamage.GetTotal().Float(),
                WasCrit     = didCrit,
            });
        }

        if (upgradeState == null)
        {
            PayDelta(target, damage, finalDamage, args.Data.Shooter);
            return;
        }

        var hitEffect = new FSProjectileHitEffectEvent
        {
            Target  = target,
            Weapon  = args.Data.Gun,
            Shooter = args.Data.Shooter,
            Damage  = finalDamage,
            State   = upgradeState,
            WasCrit = didCrit,
        };
        RaiseLocalEvent(hitEffect);

        if (hitEffect.AdditionalMultiplier != 1f)
            finalDamage *= hitEffect.AdditionalMultiplier;

        PayDelta(target, damage, finalDamage, args.Data.Shooter);
    }

    // Vanilla already dealt the base damage; only the surplus is owed.
    private void PayDelta(EntityUid target, DamageSpecifier applied, DamageSpecifier wanted, EntityUid? origin)
    {
        var delta = wanted - applied;
        if (delta.GetTotal() <= FixedPoint2.Zero)
            return;

        _damageable.TryChangeDamage(target, delta, ignoreResistances: false, origin: origin);
    }
}
