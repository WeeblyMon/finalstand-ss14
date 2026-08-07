using Content.Server._FinalStand.Crit;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared._FinalStand.Upgrades.Effects;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class ExecutionShotUpgradeSystem : EntitySystem
{
    [Dependency] private readonly CritSystem _crit = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSGunReloadedEvent>(OnReloaded);
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnReloaded(ref FSGunReloadedEvent ev)
    {
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Gun, out var state) || !state.ExecutionShotUpgradeEnabled)
            return;
        EnsureComp<FSExecutionReadyComponent>(ev.Gun);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null || ev.Shooter == null)
            return;
        if (ev.State is not { } state || !state.ExecutionShotUpgradeEnabled)
            return;
        if (!HasComp<FSExecutionReadyComponent>(ev.Weapon.Value))
            return;
        if (ev.WasCrit)
        {
            RemComp<FSExecutionReadyComponent>(ev.Weapon.Value);
            return;
        }

        ev.AdditionalMultiplier *= state.CritDamageMultiplier;
        _crit.MarkPendingCrit(ev.Shooter.Value, ev.Target);
        RemComp<FSExecutionReadyComponent>(ev.Weapon.Value);
    }
}
