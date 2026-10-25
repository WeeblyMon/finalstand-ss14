using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Server.Atmos.EntitySystems;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Atmos.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSFlamethrowerSystem : EntitySystem
{
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSTargetAcquisitionSystem _targeting = default!;
    [Dependency] private RayCastSystem _rayCast = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FlammableSystem _flammable = default!;

    private const float AcquireInterval = 0.25f;

    // Flames leave this far in front of the zombie and are this thick; a wall stops their centre a radius short.
    private const float FlameOffset = 0.3f;
    private const float FlameRadius = 0.1f;
    // Anything this close to a flame's path catches.
    private const float HitRadius = 0.5f;
    private const float CheckInterval = 0.05f;

    // Flames stop at whatever a projectile on the Impassable mask would have hit: walls, windows, shut doors.
    private static readonly QueryFilter WallFilter = new() { MaskBits = (int) CollisionGroup.Impassable };

    private readonly HashSet<Entity<FlammableComponent>> _candidates = new();

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

        var streams = EntityQueryEnumerator<FSFlameStreamComponent>();
        while (streams.MoveNext(out var uid, out var stream))
        {
            UpdateStream(uid, stream, frameTime);
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
        Dirty(uid, comp);
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
        Dirty(uid, comp);
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
            LaunchFlames(uid, comp);
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

    // A burst of flames across the cone. Each gets its stopping distance from one ray now, so it can't pass a wall
    // and nothing has to catch it touching one later.
    private void LaunchFlames(EntityUid uid, FSFlamethrowerComponent comp)
    {
        var origin = _transform.GetMapCoordinates(uid);
        if (origin.MapId == MapId.Nullspace)
            return;

        var stream = EnsureComp<FSFlameStreamComponent>(uid);
        var facingAngle = new Angle(comp.FiringDirection);
        var halfConeDeg = comp.ConeDegrees / 2f;
        var range = comp.FireProjectileSpeed * comp.FlameLifetime;

        for (var i = 0; i < comp.ParticlesPerBurst; i++)
        {
            var spread = _random.NextFloat(-halfConeDeg, halfConeDeg);
            var dir = (facingAngle + Angle.FromDegrees(spread)).ToVec();

            // Cast from the zombie's centre: a flame starting inside a wall it is pressed against would miss it.
            var reach = range;
            var hit = _rayCast.CastRayClosest(origin.MapId, origin.Position, dir * (FlameOffset + range), WallFilter);
            if (hit.Hit)
                reach = MathF.Max(0f, hit.Results[0].Fraction * (FlameOffset + range) - FlameOffset - FlameRadius);

            stream.Flames.Add(new FSFlame
            {
                Id = stream.NextId++,
                Map = origin.MapId,
                Origin = origin.Position + dir * FlameOffset,
                Direction = dir,
                Speed = comp.FireProjectileSpeed,
                Reach = reach,
                Life = comp.FlameLifetime,
                FireStacks = comp.FireStacks,
            });
        }
    }

    private void UpdateStream(EntityUid uid, FSFlameStreamComponent stream, float frameTime)
    {
        var flames = stream.Flames;
        for (var i = flames.Count - 1; i >= 0; i--)
        {
            var flame = flames[i];
            flame.Age += frameTime;
            if (flame.Age >= flame.Life)
            {
                flames[i] = flames[^1];
                flames.RemoveAt(flames.Count - 1);
                continue;
            }

            flames[i] = flame;
        }

        if (flames.Count == 0)
        {
            RemCompDeferred<FSFlameStreamComponent>(uid);
            return;
        }

        stream.CheckAccumulator += frameTime;
        if (stream.CheckAccumulator < CheckInterval)
            return;
        stream.CheckAccumulator = 0f;

        // One lookup over everything the flames swept since the last check, then each flame's path against each target.
        var map = flames[0].Map;
        var swept = Box2.Empty;
        var first = true;
        foreach (var flame in flames)
        {
            var from = flame.Origin + flame.Direction * flame.Checked;
            var to = flame.Origin + flame.Direction * flame.Travelled;
            var box = new Box2(Vector2.Min(from, to), Vector2.Max(from, to));
            swept = first ? box : swept.Union(box);
            first = false;
        }

        _candidates.Clear();
        _lookup.GetEntitiesIntersecting(map, swept.Enlarged(HitRadius), _candidates);

        foreach (var (target, flammable) in _candidates)
        {
            if (HasComp<WaveSpawnedTagComponent>(target))
                continue;

            var bounds = _lookup.GetWorldAABB(target).Enlarged(HitRadius);
            foreach (var flame in flames)
            {
                var from = flame.Origin + flame.Direction * flame.Checked;
                var to = flame.Origin + flame.Direction * flame.Travelled;
                if (!SegmentHits(from, to, bounds) || !stream.Ignited.Add((flame.Id, target)))
                    continue;

                // Uncapped on purpose: a long hosing builds stacks past the usual maximum, which is the flamer's threat.
                flammable.FireStacks += flame.FireStacks;
                _flammable.Ignite(target, uid, flammable);
            }
        }

        for (var i = 0; i < flames.Count; i++)
        {
            var flame = flames[i];
            flame.Checked = flame.Travelled;
            flames[i] = flame;
        }
    }

    // Slab test of the segment against the box.
    private static bool SegmentHits(Vector2 from, Vector2 to, Box2 box)
    {
        var delta = to - from;
        var enter = 0f;
        var exit = 1f;

        for (var axis = 0; axis < 2; axis++)
        {
            var start = axis == 0 ? from.X : from.Y;
            var step = axis == 0 ? delta.X : delta.Y;
            var min = axis == 0 ? box.Left : box.Bottom;
            var max = axis == 0 ? box.Right : box.Top;

            if (MathF.Abs(step) < 1e-6f)
            {
                if (start < min || start > max)
                    return false;
                continue;
            }

            var t1 = (min - start) / step;
            var t2 = (max - start) / step;
            if (t1 > t2)
                (t1, t2) = (t2, t1);

            enter = MathF.Max(enter, t1);
            exit = MathF.Min(exit, t2);
            if (enter > exit)
                return false;
        }

        return true;
    }

    private void StopFiring(EntityUid uid, FSFlamethrowerComponent comp)
    {
        comp.IsFiring = false;
        comp.Target = null;
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
