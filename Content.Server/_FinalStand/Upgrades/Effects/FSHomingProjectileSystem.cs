using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

// steers bolts carrying FSHomingProjectileComponent toward the nearest enemy every tick, turning at most
// TurnRateDegrees per second so higher HomingBolts levels curve in tighter/faster
public sealed class FSHomingProjectileSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private readonly ObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>> _entSetPool =
        new DefaultObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>>(
            new SetPolicy<Entity<WaveSpawnedTagComponent>>());

    private const float SearchRadius = 8f;
    // A bolt with nothing in range would otherwise run a radius query every tick for its whole life.
    private static readonly TimeSpan SearchInterval = TimeSpan.FromSeconds(0.2);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSHomingProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var homing, out var physics, out var xform))
        {
            if (homing.TurnRateDegrees <= 0)
                continue;

            if (homing.Target is not { } target || Deleted(target) || _mobState.IsDead(target))
            {
                var now = _timing.CurTime;
                if (now < homing.NextSearch)
                    continue;

                homing.NextSearch = now + SearchInterval;
                homing.Target = FindTarget(xform);
                if (homing.Target is not { } found)
                    continue;
                target = found;
            }

            var selfPos = _xform.GetWorldPosition(xform);
            var targetPos = _xform.GetWorldPosition(target);
            var toTarget = targetPos - selfPos;
            if (toTarget.LengthSquared() < 0.01f)
                continue;

            var speed = physics.LinearVelocity.Length();
            if (speed < 0.01f)
                continue;

            var currentAngle = physics.LinearVelocity.ToAngle();
            var desiredAngle = toTarget.ToAngle();

            var delta = Angle.ShortestDistance(currentAngle, desiredAngle);
            var maxStep = Angle.FromDegrees(homing.TurnRateDegrees * frameTime).Theta;
            var step = Math.Clamp(delta.Theta, -maxStep, maxStep);
            var newAngle = currentAngle + new Angle(step);
            var newDir = newAngle.ToVec();

            _physics.SetLinearVelocity(uid, newDir * speed, body: physics);
            _xform.SetWorldRotation(uid, newDir.ToWorldAngle());
        }
    }

    private EntityUid? FindTarget(TransformComponent xform)
    {
        var selfPos = _xform.GetWorldPosition(xform);
        var nearby = _entSetPool.Get();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(new MapCoordinates(selfPos, xform.MapID), SearchRadius, nearby);

        EntityUid? nearest = null;
        var nearestDistSq = float.MaxValue;
        foreach (var candidate in nearby)
        {
            if (_mobState.IsDead(candidate.Owner))
                continue;

            var distSq = (_xform.GetWorldPosition(candidate.Owner) - selfPos).LengthSquared();
            if (distSq < nearestDistSq)
            {
                nearestDistSq = distSq;
                nearest = candidate.Owner;
            }
        }
        _entSetPool.Return(nearby);

        return nearest;
    }
}
