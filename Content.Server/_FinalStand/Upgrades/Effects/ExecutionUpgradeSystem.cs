using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Server._FinalStand.Upgrades.Effects;

// 5× damage multiplier against targets below 25% max HP
public sealed class ExecutionUpgradeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;

    private const float ExecutionThreshold = 0.25f;
    private const float ExecutionMultiplier = 5.0f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || !state.ExecutionEnabled)
            return;
        if (!TryComp<DamageableComponent>(ev.Target, out var damageable))
            return;
        if (!TryComp<MobThresholdsComponent>(ev.Target, out var thresholds))
            return;

        FixedPoint2 deadThreshold = 0;
        foreach (var (hp, mobState) in thresholds.Thresholds)
        {
            if (mobState == MobState.Dead && hp > deadThreshold)
                deadThreshold = hp;
        }
        if (deadThreshold <= 0)
            return;

        var maxHp = deadThreshold.Float();
        var currentDamage = _damageable.GetPositiveDamage((ev.Target, damageable)).GetTotal().Float();
        var currentHp = maxHp - currentDamage;
        if (currentHp / maxHp >= ExecutionThreshold)
            return;

        var bonus = ev.Damage * FixedPoint2.New(ExecutionMultiplier - 1f);
        _damageable.TryChangeDamage(ev.Target, bonus, ignoreResistances: false, origin: ev.Shooter);
    }
}
