using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Station;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Server.GameObjects;
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
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSFlamethrowerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, FSFlamethrowerComponent comp, ComponentShutdown args)
    {
        comp.IsWindingUp = false;
        if (comp.FireSoundEntity.HasValue)
        {
            _audio.Stop(comp.FireSoundEntity.Value);
            comp.FireSoundEntity = null;
        }
        _pointLight.SetEnabled(uid, false);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSFlamethrowerComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsFiring)
                UpdateFiring(uid, comp, frameTime);
            else if (comp.IsWindingUp)
                UpdateWindup(uid, comp, frameTime);
            else
                UpdateIdle(uid, comp, frameTime);
        }
    }

    private void UpdateIdle(EntityUid uid, FSFlamethrowerComponent comp, float frameTime)
    {
        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Alive)
            return;

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
            if (HasComp<GhostComponent>(targetUid)) continue;
            if (TryComp<MobStateComponent>(targetUid, out var targetMobState) && targetMobState.CurrentState != MobState.Alive) continue;
            if (!_examine.InRangeUnOccluded(uid, targetUid, comp.FlameRange, null)) continue;
            var dist = Vector2.Distance(worldPos, _transform.GetWorldPosition(targetUid));
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestPlayer = targetUid;
            }
        }

        // Fallback to CCC when no player in range
        var firingTarget = nearestPlayer;
        if (firingTarget == null)
        {
            var cccSet = new HashSet<Entity<FinalStandCCCComponent>>();
            _lookup.GetEntitiesInRange<FinalStandCCCComponent>(epicenter, comp.FlameRange, cccSet);
            foreach (var (cccUid, _) in cccSet)
            {
                firingTarget = cccUid;
                break;
            }
            if (firingTarget == null)
                return;
        }

        var dir = _transform.GetWorldPosition(firingTarget.Value) - worldPos;
        if (dir != Vector2.Zero)
        {
            comp.FiringDirection = Vector2.Normalize(dir);
            _transform.SetLocalRotation(uid, new Angle(dir) - MathF.PI / 2f);
        }

        comp.IsWindingUp = true;
        comp.WindupAccumulator = 0f;
        _pointLight.SetEnabled(uid, true);

        if (TryComp<InputMoverComponent>(uid, out var mover))
        {
            mover.CanMove = false;
            Dirty(uid, mover);
        }
    }

    private void UpdateWindup(EntityUid uid, FSFlamethrowerComponent comp, float frameTime)
    {
        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Alive)
        {
            AbortWindup(uid, comp);
            return;
        }

        TrackNearestPlayer(uid, comp, frameTime);

        comp.WindupAccumulator += frameTime;
        if (comp.WindupAccumulator < comp.WindupDuration)
            return;

        comp.IsWindingUp = false;
        comp.IsFiring = true;
        comp.FireAccumulator = 0f;
        comp.ParticleAccumulator = 0f;
        comp.FireSoundEntity ??= _audio.PlayPvs(comp.FireLoopSound, uid)?.Entity;
        Dirty(uid, comp);
    }

    private void AbortWindup(EntityUid uid, FSFlamethrowerComponent comp)
    {
        comp.IsWindingUp = false;
        comp.WindupAccumulator = 0f;
        _pointLight.SetEnabled(uid, false);
        if (TryComp<InputMoverComponent>(uid, out var mover))
        {
            mover.CanMove = true;
            Dirty(uid, mover);
        }
    }

    private void UpdateFiring(EntityUid uid, FSFlamethrowerComponent comp, float frameTime)
    {
        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Alive)
        {
            StopFiring(uid, comp);
            return;
        }

        TrackNearestPlayer(uid, comp, frameTime);

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

    private void TrackNearestPlayer(EntityUid uid, FSFlamethrowerComponent comp, float frameTime)
    {
        var worldPos = _transform.GetWorldPosition(uid);
        var myMap = Transform(uid).MapID;

        EntityUid? nearest = null;
        var nearestDist = float.MaxValue;

        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var targetUid, out _, out var targetXform))
        {
            if (targetXform.MapID != myMap) continue;
            if (HasComp<WaveSpawnedTagComponent>(targetUid)) continue;
            if (HasComp<GhostComponent>(targetUid)) continue;
            if (TryComp<MobStateComponent>(targetUid, out var ms) && ms.CurrentState != MobState.Alive) continue;
            var dist = Vector2.Distance(worldPos, _transform.GetWorldPosition(targetXform));
            if (dist > comp.FlameRange || dist >= nearestDist) continue;
            nearestDist = dist;
            nearest = targetUid;
        }

        if (nearest == null)
            return;

        var toTarget = Vector2.Normalize(_transform.GetWorldPosition(nearest.Value) - worldPos);
        var currentAngle = MathF.Atan2(comp.FiringDirection.Y, comp.FiringDirection.X);
        var targetAngle = MathF.Atan2(toTarget.Y, toTarget.X);

        var delta = targetAngle - currentAngle;
        while (delta > MathF.PI) delta -= MathF.Tau;
        while (delta < -MathF.PI) delta += MathF.Tau;
        var maxStep = comp.TrackingRotationSpeed * frameTime;
        var step = Math.Clamp(delta, -maxStep, maxStep);
        var newAngle = currentAngle + step;

        comp.FiringDirection = new Vector2(MathF.Cos(newAngle), MathF.Sin(newAngle));
        _transform.SetLocalRotation(uid, new Angle(comp.FiringDirection) - MathF.PI / 2f);
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
            var spawnCoords = coords.Offset(shotDir * 0.3f);
            var projectile = Spawn("FSFireProjectile", spawnCoords);

            var body = EnsureComp<PhysicsComponent>(projectile);
            _physics.SetBodyStatus(projectile, body, BodyStatus.InAir);
            _physics.SetLinearVelocity(projectile, shotDir * comp.FireProjectileSpeed, body: body);
        }
    }

    private void StopFiring(EntityUid uid, FSFlamethrowerComponent comp)
    {
        comp.IsFiring = false;
        comp.CooldownAccumulator = comp.AttackCooldown;
        _pointLight.SetEnabled(uid, false);
        Dirty(uid, comp);

        _audio.Stop(comp.FireSoundEntity);
        comp.FireSoundEntity = null;
        if (TryComp<InputMoverComponent>(uid, out var mover))
        {
            mover.CanMove = true;
            Dirty(uid, mover);
        }

        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.Replan(htn);
    }
}
