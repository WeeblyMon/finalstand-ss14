using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSFlamethrowerSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedProjectileSystem _projectiles = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSFlamethrowerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsFiring)
                UpdateFiring(uid, comp, frameTime);
            else
                UpdateIdle(uid, comp, frameTime);
        }
    }

    private void UpdateIdle(EntityUid uid, FSFlamethrowerComponent comp, float frameTime)
    {
        if (comp.CooldownAccumulator > 0f)
        {
            comp.CooldownAccumulator -= frameTime;
            return;
        }

        var worldPos = _transform.GetWorldPosition(uid);
        var mapId = Transform(uid).MapID;
        var epicenter = new MapCoordinates(worldPos, mapId);

        var candidates = new HashSet<Entity<ActorComponent>>();
        _lookup.GetEntitiesInRange<ActorComponent>(epicenter, comp.FlameRange, candidates);

        EntityUid? nearestPlayer = null;
        float nearestDist = float.MaxValue;
        foreach (var (targetUid, _) in candidates)
        {
            if (HasComp<WaveSpawnedTagComponent>(targetUid)) continue;
            var dist = Vector2.Distance(worldPos, _transform.GetWorldPosition(targetUid));
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestPlayer = targetUid;
            }
        }

        if (nearestPlayer == null)
            return;

        var dir = _transform.GetWorldPosition(nearestPlayer.Value) - worldPos;
        if (dir != Vector2.Zero)
        {
            comp.FiringDirection = Vector2.Normalize(dir);
            // Subtract π/2 to convert standard-math (East=0) to SS14 (South=0) convention
            _transform.SetLocalRotation(uid, new Angle(dir) - MathF.PI / 2f);
        }

        comp.IsFiring = true;
        comp.FireAccumulator = 0f;
        comp.ParticleAccumulator = 0f;
        Dirty(uid, comp);

        // FINALSTAND: pause NPC movement while firing; restored in StopFiring
        if (TryComp<InputMoverComponent>(uid, out var mover))
        {
            mover.CanMove = false;
            Dirty(uid, mover);
        }

        comp.FireSoundEntity = _audio.PlayPvs(
            comp.FireLoopSound,
            uid,
            AudioParams.Default.WithLoop(true))?.Entity;
    }

    private void UpdateFiring(EntityUid uid, FSFlamethrowerComponent comp, float frameTime)
    {
        comp.FireAccumulator += frameTime;

        comp.ParticleAccumulator += frameTime;
        if (comp.ParticleAccumulator >= comp.ParticleSpawnRate)
        {
            comp.ParticleAccumulator = 0f;
            SpawnFireBurst(uid, comp);
        }

        if (comp.FireAccumulator >= comp.AttackDuration)
            StopFiring(uid, comp);
    }

    private void SpawnFireBurst(EntityUid uid, FSFlamethrowerComponent comp)
    {
        var coords = Transform(uid).Coordinates;
        var facingAngle = new Angle(comp.FiringDirection);
        var halfConeDeg = comp.ConeDegrees / 2f;

        for (var i = 0; i < comp.ParticlesPerBurst; i++)
        {
            var spread = _random.NextFloat(-halfConeDeg, halfConeDeg);
            var shotAngle = facingAngle + Angle.FromDegrees(spread);
            var shotDir = shotAngle.ToVec();

            // Spawn slightly in front of the zombie so it doesn't self-collide
            var spawnCoords = coords.Offset(shotDir * 0.6f);
            var projectile = Spawn("FSFireProjectile", spawnCoords);

            var body = EnsureComp<PhysicsComponent>(projectile);
            _physics.SetBodyStatus(projectile, body, BodyStatus.InAir);
            _physics.SetLinearVelocity(projectile, shotDir * comp.FireProjectileSpeed, body: body);

            if (TryComp<ProjectileComponent>(projectile, out var projComp))
                _projectiles.SetShooter(projectile, projComp, uid);
        }
    }

    private void StopFiring(EntityUid uid, FSFlamethrowerComponent comp)
    {
        comp.IsFiring = false;
        comp.CooldownAccumulator = comp.AttackCooldown;
        Dirty(uid, comp);

        _audio.Stop(comp.FireSoundEntity);
        comp.FireSoundEntity = null;

        // FINALSTAND: resume NPC movement after firing ends
        if (TryComp<InputMoverComponent>(uid, out var mover))
        {
            mover.CanMove = true;
            Dirty(uid, mover);
        }

        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.Replan(htn);
    }
}
