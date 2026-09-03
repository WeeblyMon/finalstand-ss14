using System.Numerics;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Stunnable;
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
    [Dependency] private KnockbackUpgradeSystem _knockback = default!;
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
    [Dependency] private IGameTiming _timing = default!;

    private readonly ObjectPool<HashSet<Entity<ActorComponent>>> _actorPool =
        new DefaultObjectPool<HashSet<Entity<ActorComponent>>>(new SetPolicy<Entity<ActorComponent>>());

    private readonly List<(EntityUid Victim, float Distance)> _victims = new();

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(1);

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
            DrawLane(ent, origin, targetPos, xform.MapID);
            return true;
        }

        if (now < comp.NextDash || distance < comp.DashMinRange || distance > comp.DashMaxRange)
            return false;

        Begin(ent, FSGiantAbility.DashWindup, comp.DashWindup, now);
        DrawLane(ent, origin, targetPos, xform.MapID);
        SpawnFist(ent, origin, targetPos, xform.MapID);
        return true;
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
        _audio.PlayPvs(comp.ImpactSound, ent.Owner);

        var blast = new DamageSpecifier();
        blast.DamageDict["Blunt"] = FixedPoint2.New(comp.SkyJumpDamage);

        CollectVictims(comp.LockedTarget, xform.MapID, comp.SkyJumpOuterRadius);
        foreach (var (victim, distance) in _victims)
        {
            if (distance <= comp.SkyJumpRadius)
            {
                _damageable.TryChangeDamage(victim, blast, origin: ent.Owner);
                _stun.TryKnockdown(victim, TimeSpan.FromSeconds(2.5));
                _knockback.ApplyKnockback(victim, ent.Owner, 3);
            }
            else
            {
                _knockback.ApplyKnockback(victim, ent.Owner, 2);
                Slow(victim, 0.55f, 3f);
            }

            Shake(victim, comp.LockedTarget, 1f - distance / comp.SkyJumpOuterRadius);
        }

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
            _audio.PlayPvs(comp.RoarSound, ent.Owner);
        }

        comp.NextBoulder = now + TimeSpan.FromSeconds(comp.BoulderCooldown);
        Finish(ent, now);
    }

    private void Dash(Entity<FSGiantAbilitiesComponent> ent, TransformComponent xform, TimeSpan now)
    {
        var comp = ent.Comp;
        var origin = _transform.GetWorldPosition(xform);
        var direction = comp.LockedTarget - origin;
        var mapId = xform.MapID;

        comp.NextDash = now + TimeSpan.FromSeconds(comp.DashCooldown);

        if (direction.LengthSquared() < 0.01f)
        {
            Finish(ent, now);
            return;
        }

        var heading = direction.Normalized();
        var landing = origin;

        for (var step = 1; step <= 6; step++)
        {
            var probe = origin + heading * (comp.DashDistance * step / 6f);
            if (!_interaction.InRangeUnobstructed(new MapCoordinates(origin, mapId), new MapCoordinates(probe, mapId)))
                break;

            landing = probe;
            Spawn(comp.LaneProto, new MapCoordinates(probe, mapId));
        }

        _transform.SetWorldPosition(ent.Owner, landing);
        _audio.PlayPvs(comp.ImpactSound, ent.Owner);

        var punch = new DamageSpecifier();
        punch.DamageDict["Blunt"] = FixedPoint2.New(comp.DashDamage);

        CollectVictims(landing, mapId, comp.DashHitRadius);
        foreach (var (victim, _) in _victims)
        {
            _damageable.TryChangeDamage(victim, punch, origin: ent.Owner);
            _knockback.ApplyKnockback(victim, ent.Owner, 5);
            _stun.TryUpdateStunDuration(victim, TimeSpan.FromSeconds(1.5));
            Shake(victim, landing, 1f);
        }

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

    private void DrawLane(Entity<FSGiantAbilitiesComponent> ent, Vector2 origin, Vector2 target, MapId mapId)
    {
        var direction = target - origin;
        var length = direction.Length();
        if (length < 0.5f)
            return;

        var heading = direction / length;
        var steps = Math.Min(20, (int) length);

        for (var i = 1; i <= steps; i++)
            ent.Comp.LaneEntities.Add(Spawn(ent.Comp.LaneProto, new MapCoordinates(origin + heading * i, mapId)));
    }

    private void SpawnFist(Entity<FSGiantAbilitiesComponent> ent, Vector2 origin, Vector2 target, MapId mapId)
    {
        var direction = target - origin;
        if (direction.LengthSquared() < 0.01f)
            return;

        var fist = Spawn(ent.Comp.FistProto, new MapCoordinates(origin + direction.Normalized(), mapId));
        _transform.SetWorldRotation(fist, direction.ToWorldAngle());
        ent.Comp.LaneEntities.Add(fist);
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

    private void Slow(EntityUid target, float factor, float seconds)
    {
        var slow = EnsureComp<FSSlowedComponent>(target);
        slow.SlowFactor = factor;
        slow.EndTime = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        _movement.RefreshMovementSpeedModifiers(target);
    }

    private void Shake(EntityUid target, Vector2 origin, float magnitude)
    {
        if (magnitude <= 0.01f)
            return;

        var direction = _transform.GetWorldPosition(target) - origin;
        if (direction == Vector2.Zero)
            direction = new Vector2(1f, 0f);

        _recoil.KickCamera(target, direction.Normalized() * magnitude);
    }
}
