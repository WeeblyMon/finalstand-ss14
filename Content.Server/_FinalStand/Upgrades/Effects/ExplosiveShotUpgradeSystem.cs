using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
using Content.Server._FinalStand.Spawners;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed partial class ExplosiveShotUpgradeSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ExplosionSystem _explosion = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private KnockbackUpgradeSystem _knockback = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;


    private readonly ObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>> _entSetPool =
        new DefaultObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>>(
            new SetPolicy<Entity<WaveSpawnedTagComponent>>());

    private const float AoeRadius = 2.5f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (ev.State is not { } state || state.ExplosiveShotLevel <= 0)
            return;
        if (!HasComp<WaveSpawnedTagComponent>(ev.Target))
            return;

        var splashDamage = ev.Damage * state.ExplosiveShotLevel;

        var targetPos = _transform.GetWorldPosition(ev.Target);
        var mapId = Transform(ev.Target).MapID;
        var epicenter = new MapCoordinates(targetPos, mapId);

        _explosion.QueueExplosion(
            epicenter,
            "FSExplosiveShotExplosion",
            totalIntensity: 2f,
            slope: 5f,
            maxTileIntensity: 1f,
            cause: ev.Shooter,
            tileBreakScale: 0f,
            maxTileBreak: 0,
            canCreateVacuum: false,
            addLog: false);

        var nearby = _entSetPool.Get();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(new MapCoordinates(targetPos, mapId), AoeRadius, nearby);

        var primaryTarget = ev.Target;
        var shooter = ev.Shooter;

        foreach (var splashTarget in nearby)
        {
            if (splashTarget.Owner == primaryTarget || _mobState.IsDead(splashTarget.Owner))
                continue;

            _damageable.TryChangeDamage(splashTarget.Owner, splashDamage, ignoreResistances: false, origin: shooter);
            if (state.KnockbackLevel > 0 && shooter != null)
                _knockback.ApplyKnockback(splashTarget.Owner, shooter.Value, state.KnockbackLevel);
        }
        _entSetPool.Return(nearby);
    }
}
