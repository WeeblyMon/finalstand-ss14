using Content.Server._FinalStand.Crit;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class PointBlankCritUpgradeSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly CritSystem _crit = default!;

    private const float PointBlankRange = 2.0f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null || ev.Shooter == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || !state.PointBlankCritEnabled)
            return;
        if (ev.WasCrit)
            return;

        var shooterPos = _transform.GetWorldPosition(ev.Shooter.Value);
        var targetPos = _transform.GetWorldPosition(ev.Target);
        if ((targetPos - shooterPos).Length() > PointBlankRange)
            return;

        ev.AdditionalMultiplier *= state.CritDamageMultiplier;
        _crit.MarkPendingCrit(ev.Shooter.Value, ev.Target);
    }
}
