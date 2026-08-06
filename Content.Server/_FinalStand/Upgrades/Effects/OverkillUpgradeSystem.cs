using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Upgrades.Effects;

// on kill, excess damage (damage dealt beyond remaining HP) transfers to the nearest enemy within range
public sealed class OverkillUpgradeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float TransferRangeL1 = 5f;
    private const float TransferRangeL2 = 8f;
    private const float TransferRangeL3 = 12f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || state.OverkillLevel <= 0)
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

        var currentDamage = _damageable.GetPositiveDamage((ev.Target, damageable)).GetTotal();
        var hitTotal = ev.Damage.GetTotal();
        // A target already past its dead threshold gives a negative remainder, which a zero-damage
        // hit clears. Without this the ratio below divides by zero and transfers NaN damage.
        if (hitTotal <= FixedPoint2.Zero)
            return;

        var remaining = deadThreshold - currentDamage;
        if (hitTotal <= remaining)
            return;

        var excess = hitTotal - remaining;
        var ratio = FixedPoint2.New(excess.Float() / hitTotal.Float());
        var excessDamage = ev.Damage * ratio;

        var range = state.OverkillLevel switch
        {
            1 => TransferRangeL1,
            2 => TransferRangeL2,
            _ => TransferRangeL3,
        };

        var targetPos = _xform.GetWorldPosition(ev.Target);
        var mapId = Transform(ev.Target).MapID;
        var nearby = new HashSet<Entity<WaveSpawnedTagComponent>>();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(new MapCoordinates(targetPos, mapId), range, nearby);

        foreach (var candidate in nearby)
        {
            if (candidate.Owner == ev.Target)
                continue;
            if (_mobState.IsDead(candidate.Owner))
                continue;
            _damageable.TryChangeDamage(candidate.Owner, excessDamage, ignoreResistances: false, origin: ev.Shooter);
            break;
        }
    }
}
