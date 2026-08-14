using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
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
public sealed partial class OverkillUpgradeSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedTransformSystem _xform = default!;


    private readonly ObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>> _entSetPool =
        new DefaultObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>>(
            new SetPolicy<Entity<WaveSpawnedTagComponent>>());

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
        if (ev.State is not { } state || state.OverkillLevel <= 0)
            return;
        if (!TryComp<DamageableComponent>(ev.Target, out var damageable))
            return;
        if (!_thresholds.TryGetDeadThreshold(ev.Target, out var dead) || dead.Value <= 0)
            return;
        var deadThreshold = dead.Value;

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
        var nearby = _entSetPool.Get();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(new MapCoordinates(targetPos, mapId), range, nearby);

        // Nearest, not first: the set is unordered, so taking the first hit transferred the
        // overkill to an arbitrary enemy in range.
        EntityUid? nearest = null;
        var nearestDistSq = float.MaxValue;
        foreach (var candidate in nearby)
        {
            if (candidate.Owner == ev.Target || _mobState.IsDead(candidate.Owner))
                continue;

            var distSq = (_xform.GetWorldPosition(candidate.Owner) - targetPos).LengthSquared();
            if (distSq >= nearestDistSq)
                continue;
            nearestDistSq = distSq;
            nearest = candidate.Owner;
        }
        _entSetPool.Return(nearby);

        if (nearest is { } victim)
            _damageable.TryChangeDamage(victim, excessDamage, ignoreResistances: false, origin: ev.Shooter);
    }
}
