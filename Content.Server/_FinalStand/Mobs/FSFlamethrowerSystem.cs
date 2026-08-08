using System.Numerics;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
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
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly FSTargetAcquisitionSystem _targeting = default!;

    private const string GlowProto = "FSFlamethrowerGlow";

    private const float AcquireInterval = 0.25f;

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
        DespawnGlow(comp);
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

        comp.AcquireAccumulator += frameTime;
        if (comp.AcquireAccumulator < AcquireInterval)
            return;
        comp.AcquireAccumulator = 0f;

        if (_targeting.AcquireTarget(uid, comp.FlameRange) is not { } firingTarget)
            return;

        comp.Target = firingTarget;

        var worldPos = _transform.GetWorldPosition(uid);
        var dir = _transform.GetWorldPosition(firingTarget) - worldPos;
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
        comp.GlowEntity ??= Spawn(GlowProto, Transform(uid).Coordinates);
        MoveGlow(uid, comp);
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

        MoveGlow(uid, comp);

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

        // Re-acquire only when the locked target is gone, dead, or out of tracking range.
        if (!IsTrackable(uid, comp, comp.Target, worldPos))
            comp.Target = _targeting.FindNearestPlayer(uid, comp.TrackingRange, requireLineOfSight: false);

        if (comp.Target is not { } target)
            return;

        var toTargetRaw = _transform.GetWorldPosition(target) - worldPos;
        if (toTargetRaw.LengthSquared() < 0.001f)
            return;

        var toTarget = Vector2.Normalize(toTargetRaw);
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

    private bool IsTrackable(EntityUid uid, FSFlamethrowerComponent comp, EntityUid? target, Vector2 worldPos)
    {
        if (target is not { } t || !Exists(t) || Deleted(t))
            return false;
        if (TryComp<MobStateComponent>(t, out var mobState) && mobState.CurrentState != MobState.Alive)
            return false;

        return Vector2.DistanceSquared(worldPos, _transform.GetWorldPosition(t))
               <= comp.TrackingRange * comp.TrackingRange;
    }

    private void MoveGlow(EntityUid uid, FSFlamethrowerComponent comp)
    {
        if (comp.GlowEntity is not { } glow || !Exists(glow))
            return;

        var ahead = comp.FiringDirection * (comp.FlameRange * 0.5f);
        _transform.SetCoordinates(glow, Transform(uid).Coordinates.Offset(ahead));
    }

    private void DespawnGlow(FSFlamethrowerComponent comp)
    {
        if (comp.GlowEntity is { } glow && Exists(glow))
            QueueDel(glow);
        comp.GlowEntity = null;
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
        comp.Target = null;
        comp.CooldownAccumulator = comp.AttackCooldown;
        DespawnGlow(comp);
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
