using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Upgrades.Effects;

// on a miss (bolt hits a wall/structure, not an enemy), proc chance to redirect a new bolt at the nearest enemy
public sealed class RicochetUpgradeSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const string BoltProto = "FSBulletLaserCarbine";
    private const float BoltSpeed = 20f;
    private const float SeekRange = 10f;
    private const float DamageRatio = 0.7f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null || ev.Shooter == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || state.RicochetLevel <= 0)
            return;

        // already hit a real enemy - nothing to rescue
        if (HasComp<WaveSpawnedTagComponent>(ev.Target))
            return;

        if (!_random.Prob(state.RicochetLevel * 0.25f))
            return;

        var targetPos = _xform.GetWorldPosition(ev.Target);
        var mapId = Transform(ev.Target).MapID;
        var nearby = new HashSet<Entity<WaveSpawnedTagComponent>>();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(new MapCoordinates(targetPos, mapId), SeekRange, nearby);

        Entity<WaveSpawnedTagComponent>? nearest = null;
        var nearestDistSq = float.MaxValue;
        foreach (var candidate in nearby)
        {
            if (_mobState.IsDead(candidate.Owner))
                continue;

            var distSq = (_xform.GetWorldPosition(candidate.Owner) - targetPos).LengthSquared();
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearest = candidate;
            }
        }

        if (nearest == null)
            return;

        var toEnemy = _xform.GetWorldPosition(nearest.Value.Owner) - targetPos;
        if (toEnemy.LengthSquared() < 0.001f)
            return;
        var dir = Vector2.Normalize(toEnemy);

        var bolt = Spawn(BoltProto, Transform(ev.Target).Coordinates);
        if (TryComp<ProjectileComponent>(bolt, out var proj))
            proj.Damage = ev.Damage * FixedPoint2.New(DamageRatio);

        _gun.ShootProjectile(bolt, dir, Vector2.Zero, ev.Shooter.Value, null, BoltSpeed);
    }
}
