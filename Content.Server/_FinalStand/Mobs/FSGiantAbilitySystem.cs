using System.Numerics;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Upgrades.Effects;
using Robust.Shared.Physics.Components;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSGiantAbilitySystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly ObjectPool<HashSet<Entity<ActorComponent>>> _actorPool =
        new DefaultObjectPool<HashSet<Entity<ActorComponent>>>(new SetPolicy<Entity<ActorComponent>>());

    private readonly List<(EntityUid Victim, float Distance)> _victims = new();
    private readonly HashSet<EntityUid> _swept = new();

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

    private const int DashProbeSteps = 8;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSGiantAbilitiesComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (!_mobState.IsAlive(uid))
            {
                if (comp.Current != FSGiantAbility.None)
                    Abort((uid, comp));
                continue;
            }

            if (comp.Current != FSGiantAbility.None)
            {
                if (comp.Current == FSGiantAbility.DashTravel && now < comp.PhaseEnd)
                {
                    var progress = 1f - (float) (comp.PhaseEnd - now).TotalSeconds / comp.DashTravelTime;
                    _transform.SetWorldPosition(uid, Vector2.Lerp(comp.DashOrigin, comp.DashLanding, progress));
                    continue;
                }

                if (now >= comp.PhaseEnd)
                    Advance((uid, comp), xform, now);
                continue;
            }

            if (now < comp.NextAbility)
                continue;

            if (!TrySelect((uid, comp), xform, now))
                comp.NextAbility = now + RetryDelay;
        }
    }

    private bool TrySelect(Entity<FSGiantAbilitiesComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var comp = ent.Comp;
        var origin = _transform.GetWorldPosition(xform);

        if (FindTarget(origin, xform.MapID, comp.SkyJumpMaxRange) is not { } target)
            return false;

        var targetPos = _transform.GetWorldPosition(target);
        var distance = Vector2.Distance(origin, targetPos);
        comp.LockedTarget = targetPos;

        if (now >= comp.NextSkyJump && distance >= comp.SkyJumpMinRange && distance <= comp.SkyJumpMaxRange)
        {
            Begin(ent, FSGiantAbility.SkyJumpWindup, comp.SkyJumpWindup, now);
            Spawn(comp.LaunchEffect, xform.Coordinates);
            _audio.PlayPvs(comp.RoarSound, ent.Owner);
            return true;
        }

        if (now >= comp.NextBoulder && distance >= comp.BoulderMinRange && distance <= comp.BoulderMaxRange)
        {
            Begin(ent, FSGiantAbility.BoulderWindup, comp.BoulderWindup, now);
            var lane = origin + (targetPos - origin).Normalized() * comp.BoulderMaxRange;
            DrawLane(ent, origin, lane, xform.MapID, 1.2f);
            return true;
        }

        if (now < comp.NextDash || distance < comp.DashMinRange || distance > comp.DashMaxRange)
            return false;

        comp.DashHeading = (targetPos - origin).Normalized();
        comp.DashOrigin = origin;
        comp.DashLanding = ProbeDash(origin, comp.DashHeading, comp.DashDistance, xform.MapID);

        Begin(ent, FSGiantAbility.DashWindup, comp.DashWindup, now);
        DrawLane(ent, origin, comp.DashLanding, xform.MapID, 0.9f);
        return true;
    }

    // Walks the dash line and returns the furthest point still in the clear.
    private Vector2 ProbeDash(Vector2 origin, Vector2 heading, float distance, MapId mapId)
    {
        var originCoords = new MapCoordinates(origin, mapId);
        var landing = origin;

        for (var step = 1; step <= DashProbeSteps; step++)
        {
            var probe = origin + heading * (distance * step / DashProbeSteps);
            if (!_interaction.InRangeUnobstructed(originCoords, new MapCoordinates(probe, mapId), distance + 1f))
                break;

            landing = probe;
        }

        return landing;
    }

    private void Advance(Entity<FSGiantAbilitiesComponent> ent, TransformComponent xform, TimeSpan now)
    {
        switch (ent.Comp.Current)
        {
            case FSGiantAbility.SkyJumpWindup:
                Launch(ent, xform, now);
                break;
            case FSGiantAbility.SkyJumpAir:
                Land(ent, xform, now);
                break;
            case FSGiantAbility.BoulderWindup:
                ThrowBoulder(ent, xform, now);
                break;
            case FSGiantAbility.DashWindup:
                Dash(ent, xform, now);
                break;
            case FSGiantAbility.DashTravel:
                LandDash(ent, xform, now);
                break;
        }
    }

    private void Begin(Entity<FSGiantAbilitiesComponent> ent, FSGiantAbility phase, float duration, TimeSpan now)
    {
        ent.Comp.Current = phase;
        ent.Comp.PhaseEnd = now + TimeSpan.FromSeconds(duration);
    }

    private void Finish(Entity<FSGiantAbilitiesComponent> ent, TimeSpan now)
    {
        ent.Comp.Current = FSGiantAbility.None;
        ent.Comp.NextAbility = now + TimeSpan.FromSeconds(ent.Comp.GlobalCooldown);
        ClearMarkers(ent);
    }

    private void Launch(Entity<FSGiantAbilitiesComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var comp = ent.Comp;

        comp.Airborne = true;
        Dirty(ent);
        _appearance.SetData(ent.Owner, FSGiantAbilityVisuals.Airborne, true);
        _physics.SetCanCollide(ent.Owner, false);
        EnsureComp<FSPlayerDamageImmuneComponent>(ent);

        _audio.PlayPvs(comp.LaunchSound, ent.Owner);
        comp.TelegraphEntity = Spawn(comp.TelegraphProto, new MapCoordinates(comp.LockedTarget, xform.MapID));
        Begin(ent, FSGiantAbility.SkyJumpAir, comp.SkyJumpAirTime, now);
    }

    private void Land(Entity<FSGiantAbilitiesComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var comp = ent.Comp;
        var landing = new MapCoordinates(comp.LockedTarget, xform.MapID);

        _transform.SetWorldPosition(ent.Owner, comp.LockedTarget);
        _physics.SetCanCollide(ent.Owner, true);
        RemComp<FSPlayerDamageImmuneComponent>(ent);
        comp.Airborne = false;
        Dirty(ent);
        _appearance.SetData(ent.Owner, FSGiantAbilityVisuals.Airborne, false);

        Spawn(comp.ImpactEffect, landing);
        _audio.PlayPvs(comp.LandSound, ent.Owner);
        _audio.PlayPvs(comp.ImpactSound, ent.Owner);

        var blast = new DamageSpecifier();
        blast.DamageDict["Blunt"] = FixedPoint2.New(comp.SkyJumpDamage);

        CollectVictims(comp.LockedTarget, xform.MapID, comp.SkyJumpOuterRadius);
        foreach (var (victim, distance) in _victims)
        {
            // Knockback first: a downed body has too much friction to be thrown anywhere.
            var away = _transform.GetWorldPosition(victim) - comp.LockedTarget;

            if (distance <= comp.SkyJumpRadius)
            {
                Shove(victim, away, comp.SkyJumpKnockbackDistance, comp.SkyJumpKnockbackSpeed);
                _damageable.TryChangeDamage(victim, blast, origin: ent.Owner);
            }
            else
            {
                Shove(victim, away, comp.SkyJumpKnockbackDistance * 0.5f, comp.SkyJumpKnockbackSpeed);
                Slow(victim, 0.55f, 3f);
            }
        }

        ShakeArea(comp.LockedTarget, xform.MapID, comp.ShakeRadius);

        comp.NextSkyJump = now + TimeSpan.FromSeconds(comp.SkyJumpCooldown);
        Finish(ent, now);
    }

    private void ThrowBoulder(Entity<FSGiantAbilitiesComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var comp = ent.Comp;
        var direction = comp.LockedTarget - _transform.GetWorldPosition(xform);

        if (direction.LengthSquared() > 0.01f)
        {
            var boulder = Spawn(comp.BoulderProto, xform.Coordinates);
            _gun.ShootProjectile(boulder, direction, Vector2.Zero, ent.Owner, ent.Owner, comp.BoulderSpeed);
            _audio.PlayPvs(comp.BoulderThrowSound, ent.Owner);
        }

        comp.NextBoulder = now + TimeSpan.FromSeconds(comp.BoulderCooldown);
        Finish(ent, now);
    }

    private void Dash(Entity<FSGiantAbilitiesComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var comp = ent.Comp;
        comp.NextDash = now + TimeSpan.FromSeconds(comp.DashCooldown);

        if (comp.DashHeading.LengthSquared() < 0.01f)
        {
            Finish(ent, now);
            return;
        }

        // Re-probe from where the giant actually stands now, keeping the heading it committed to.
        comp.DashOrigin = _transform.GetWorldPosition(xform);
        comp.DashLanding = ProbeDash(comp.DashOrigin, comp.DashHeading, comp.DashDistance, xform.MapID);
        _audio.PlayPvs(comp.DashSound, ent.Owner);
        Begin(ent, FSGiantAbility.DashTravel, comp.DashTravelTime, now);
    }

    private void LandDash(Entity<FSGiantAbilitiesComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var comp = ent.Comp;
        var mapId = xform.MapID;
        var landing = comp.DashLanding;
        var heading = comp.DashHeading;

        _transform.SetWorldPosition(ent.Owner, landing);
        SpawnFist(ent, landing, heading, mapId);
        _audio.PlayPvs(comp.PunchSound, ent.Owner);

        var punch = new DamageSpecifier();
        punch.DamageDict["Blunt"] = FixedPoint2.New(comp.DashDamage);

        // Sweep the whole dash line: anyone run through counts, not just whoever is at the end.
        _swept.Clear();
        var travelled = (landing - comp.DashOrigin).Length();
        var samples = Math.Max(1, (int) MathF.Ceiling(travelled));

        for (var i = 0; i <= samples; i++)
        {
            var point = Vector2.Lerp(comp.DashOrigin, landing + heading * 0.5f, (float) i / samples);
            CollectVictims(point, mapId, comp.DashHitRadius);

            foreach (var (victim, _) in _victims)
            {
                if (!_swept.Add(victim))
                    continue;

                Shove(victim, heading, comp.DashKnockbackDistance, comp.DashKnockbackSpeed);
                _damageable.TryChangeDamage(victim, punch, origin: ent.Owner);
            }
        }

        ShakeArea(landing, mapId, comp.ShakeRadius);
        Finish(ent, now);
    }

    private void Abort(Entity<FSGiantAbilitiesComponent> ent)
    {
        if (ent.Comp.Airborne)
        {
            ent.Comp.Airborne = false;
            Dirty(ent);
            _appearance.SetData(ent.Owner, FSGiantAbilityVisuals.Airborne, false);
            _physics.SetCanCollide(ent.Owner, true);
            RemComp<FSPlayerDamageImmuneComponent>(ent);
        }

        ent.Comp.Current = FSGiantAbility.None;
        ClearMarkers(ent);
    }

    private void ClearMarkers(Entity<FSGiantAbilitiesComponent> ent)
    {
        if (ent.Comp.TelegraphEntity is { } telegraph)
        {
            QueueDel(telegraph);
            ent.Comp.TelegraphEntity = null;
        }

        foreach (var marker in ent.Comp.LaneEntities)
            QueueDel(marker);

        ent.Comp.LaneEntities.Clear();
    }

    private void DrawLane(Entity<FSGiantAbilitiesComponent> ent, Vector2 origin, Vector2 target, MapId mapId, float width)
    {
        var direction = target - origin;
        var length = direction.Length();
        if (length < 0.5f)
            return;

        var lane = Spawn(ent.Comp.LaneProto, new MapCoordinates(origin + direction * 0.5f, mapId));
        _transform.SetWorldRotation(lane, direction.ToWorldAngle());

        var comp = EnsureComp<FSGiantLaneComponent>(lane);
        comp.Length = length;
        comp.Width = width;
        Dirty(lane, comp);

        ent.Comp.LaneEntities.Add(lane);
    }

    // Thrown at the end of the dash, so the punch lands with the giant rather than telegraphing it.
    private void SpawnFist(Entity<FSGiantAbilitiesComponent> ent, Vector2 landing, Vector2 heading, MapId mapId)
    {
        var fist = Spawn(ent.Comp.FistProto, new MapCoordinates(landing + heading * 1.3f, mapId));
        _transform.SetWorldRotation(fist, heading.ToWorldAngle());
    }

    private EntityUid? FindTarget(Vector2 origin, MapId mapId, float range)
    {
        var candidates = _actorPool.Get();
        _lookup.GetEntitiesInRange<ActorComponent>(new MapCoordinates(origin, mapId), range, candidates);

        EntityUid? best = null;
        var bestDistance = float.MaxValue;

        foreach (var (candidate, _) in candidates)
        {
            if (!_mobState.IsAlive(candidate))
                continue;

            var distance = Vector2.DistanceSquared(origin, _transform.GetWorldPosition(candidate));
            if (distance >= bestDistance)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        _actorPool.Return(candidates);
        return best;
    }

    private void CollectVictims(Vector2 origin, MapId mapId, float radius)
    {
        _victims.Clear();

        var candidates = _actorPool.Get();
        _lookup.GetEntitiesInRange<ActorComponent>(new MapCoordinates(origin, mapId), radius, candidates);

        foreach (var (candidate, _) in candidates)
        {
            if (!_mobState.IsAlive(candidate))
                continue;

            _victims.Add((candidate, Vector2.Distance(origin, _transform.GetWorldPosition(candidate))));
        }

        _actorPool.Return(candidates);
    }

    // Setting velocity directly gets eaten by tile friction within a few ticks, which reads as a
    // jostle rather than a launch. A friction-compensated throw actually covers the distance.
    private void Shove(EntityUid target, Vector2 direction, float distance, float speed)
    {
        if (HasComp<FSKnockedBackComponent>(target) || !HasComp<PhysicsComponent>(target))
            return;

        if (TryComp<FSKnockbackResistComponent>(target, out var resist))
        {
            distance *= resist.Multiplier;
            speed *= resist.Multiplier;
        }

        if (distance <= 0f || speed <= 0f)
            return;

        if (direction.LengthSquared() < 0.001f)
            direction = new Vector2(0f, -1f);

        var heading = Vector2.Normalize(direction);
        _throwing.TryThrow(target, heading * distance, speed, compensateFriction: true,
            recoil: false, animated: false, playSound: false, doSpin: false);

        var comp = EnsureComp<FSKnockedBackComponent>(target);
        comp.EndTime = _timing.CurTime + TimeSpan.FromSeconds(distance / speed);
        Dirty(target, comp);
    }

    private void Slow(EntityUid target, float factor, float seconds)
    {
        var slow = EnsureComp<FSSlowedComponent>(target);
        slow.SlowFactor = factor;
        slow.EndTime = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        _movement.RefreshMovementSpeedModifiers(target);
    }

    private void ShakeArea(Vector2 origin, MapId mapId, float radius)
    {
        var watchers = _actorPool.Get();
        _lookup.GetEntitiesInRange<ActorComponent>(new MapCoordinates(origin, mapId), radius, watchers);

        foreach (var (watcher, _) in watchers)
        {
            var direction = _transform.GetWorldPosition(watcher) - origin;
            var magnitude = 1f - MathF.Min(direction.Length() / radius, 1f);
            if (magnitude <= 0.02f)
                continue;

            if (direction == Vector2.Zero)
                direction = new Vector2(1f, 0f);

            _recoil.KickCamera(watcher, direction.Normalized() * magnitude);
        }

        _actorPool.Return(watchers);
    }
}
