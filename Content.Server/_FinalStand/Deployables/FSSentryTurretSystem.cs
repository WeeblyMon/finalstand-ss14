using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Deployables;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._FinalStand.Deployables;

public sealed partial class FSSentryTurretSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private FSDeployableAlertSystem _alerts = default!;

    private readonly ObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>> _enemyPool =
        new DefaultObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>>(
            new SetPolicy<Entity<WaveSpawnedTagComponent>>());

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSSentryTurretComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var turret, out var xform))
        {
            if (!xform.Anchored || now < turret.NextFire)
                continue;

            if (turret.Ammo <= 0)
            {
                turret.HasTarget = false;
                _appearance.SetData(uid, FSSentryTurretVisuals.Firing, false);
                continue;
            }

            turret.NextFire = now + TimeSpan.FromSeconds(turret.FireInterval);

            var origin = _transform.GetWorldPosition(xform);
            var target = FindTarget(origin, xform.MapID, turret.Range);

            if (target is not { } victim)
            {
                turret.HasTarget = false;
                _appearance.SetData(uid, FSSentryTurretVisuals.Firing, false);
                continue;
            }

            if (!turret.HasTarget)
            {
                turret.HasTarget = true;
                _audio.PlayPvs(turret.TargetAcquiredSound, uid);
            }

            var direction = _transform.GetWorldPosition(victim) - origin;
            if (direction.LengthSquared() < 0.001f)
                continue;

            Fire(uid, turret, xform, direction);
        }
    }

    private EntityUid? FindTarget(Vector2 origin, MapId mapId, float range)
    {
        var candidates = _enemyPool.Get();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(new MapCoordinates(origin, mapId), range, candidates);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        foreach (var (candidate, _) in candidates)
        {
            if (!_mobState.IsAlive(candidate))
                continue;

            var distance = (_transform.GetWorldPosition(candidate) - origin).LengthSquared();
            if (distance >= bestDistance)
                continue;

            if (!_interaction.InRangeUnobstructed(new MapCoordinates(origin, mapId), candidate, range))
                continue;

            best = candidate;
            bestDistance = distance;
        }

        _enemyPool.Return(candidates);
        return best;
    }

    private void Fire(EntityUid uid, FSSentryTurretComponent turret, TransformComponent xform, Vector2 direction)
    {
        var projectile = Spawn(turret.ProjectileProto, xform.Coordinates);
        EnsureComp<FSDeployableProjectileComponent>(projectile);

        if (TryComp<ProjectileComponent>(projectile, out var proj))
            proj.Damage *= turret.DamageMultiplier;

        var shooter = TryComp<FSDeployedByComponent>(uid, out var deployed)
                      && deployed.DeployedBy is { } owner
                      && !TerminatingOrDeleted(owner)
            ? owner
            : uid;
        _gun.ShootProjectile(projectile, direction, Vector2.Zero, uid, shooter, turret.ProjectileSpeed);
        _audio.PlayPvs(turret.FireSound, uid, AudioParams.Default.WithVolume(-6f));
        EjectCasing(turret, xform, direction);

        var facing = direction.ToWorldAngle() + Angle.FromDegrees(turret.SpriteAngleOffset);
        _appearance.SetData(uid, FSSentryTurretVisuals.Angle, facing.Theta);
        _appearance.SetData(uid, FSSentryTurretVisuals.Firing, true);

        turret.Ammo--;
        Dirty(uid, turret);
        _alerts.AmmoSpent(uid, turret);
    }

    private void EjectCasing(FSSentryTurretComponent turret, TransformComponent xform, Vector2 direction)
    {
        if (turret.CasingProto is not { } proto)
            return;

        var back = Angle.FromDegrees(_random.NextFloat(-25f, 25f)).RotateVec(-Vector2.Normalize(direction));
        var casing = Spawn(proto, _transform.GetMapCoordinates(xform));
        _transform.SetWorldRotation(casing, _random.NextAngle());
        _throwing.TryThrow(casing, back * _random.NextFloat(0.9f, 1.5f), baseThrowSpeed: 7f,
            recoil: false, playSound: false, compensateFriction: true);
    }
}
