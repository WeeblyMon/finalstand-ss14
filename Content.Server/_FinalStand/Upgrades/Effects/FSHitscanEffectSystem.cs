using Content.Server._FinalStand.Crit;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Weapons.Hitscan.Components;

namespace Content.Server._FinalStand.Upgrades.Effects;

// Bridges hitscan into the upgrade-effect pipeline. The shot is scaled before vanilla deals it, so a multiplier
// below 1 (an armoured deflect, Pacifist) cuts the damage like it does a projectile's. The damage number comes from
// CritSystem's DamageChangedEvent, flagged by the pending crit mark.
public sealed partial class FSHitscanEffectSystem : EntitySystem
{
    [Dependency] private CritSystem _crit = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HitscanBasicDamageComponent, FSHitscanDamageEvent>(OnHitscanDamage);
    }

    private void OnHitscanDamage(EntityUid uid, HitscanBasicDamageComponent _, ref FSHitscanDamageEvent args)
    {
        if (args.Data.HitEntity is not { } target)
            return;

        var damage = args.Damage;
        TryComp<FSWeaponUpgradeStateComponent>(args.Data.Gun, out var upgradeState);
        if (upgradeState is { DamageMultiplier: > 1.0f })
            damage *= upgradeState.DamageMultiplier;

        var didCrit = false;
        if (args.Data.Shooter is { } shooter
            && _crit.TryRollCrit(shooter, args.Data.Gun, target, out var critMultiplier))
        {
            didCrit = true;
            damage *= critMultiplier;
            _crit.MarkPendingCrit(shooter, target);
        }

        var hitEffect = new FSProjectileHitEffectEvent(target, damage)
        {
            Weapon  = args.Data.Gun,
            Shooter = args.Data.Shooter,
            State   = upgradeState,
            WasCrit = didCrit,
        };
        RaiseLocalEvent(ref hitEffect);

        if (hitEffect.AdditionalMultiplier != 1f)
            damage *= hitEffect.AdditionalMultiplier;

        args.Damage = damage;
    }
}
