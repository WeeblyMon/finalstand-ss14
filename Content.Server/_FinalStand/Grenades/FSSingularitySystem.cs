using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Grenades;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Map;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Utility;

namespace Content.Server._FinalStand.Grenades;

public sealed class FSSingularitySystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float DamageInterval = 0.1f;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    private readonly ObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>> _targetSetPool =
        new DefaultObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>>(
            new SetPolicy<Entity<WaveSpawnedTagComponent>>());

    public override void Initialize()
    {
        base.Initialize();
        // The mover controller overwrites linear velocity every tick, so the pull has to land after it.
        UpdatesAfter.Add(typeof(SharedMoverController));
        _physicsQuery = GetEntityQuery<PhysicsComponent>();
        SubscribeLocalEvent<FSSingularityComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSSingularityComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, FSSingularityComponent comp, ComponentStartup args)
    {
        SetPhase(uid, comp, FSSingularityPhase.Start);
        comp.HumStream = _audio.PlayPvs(comp.HumSound, uid)?.Entity;
    }

    private void OnShutdown(EntityUid uid, FSSingularityComponent comp, ComponentShutdown args)
    {
        StopHum(comp);
    }

    private void StopHum(FSSingularityComponent comp)
    {
        if (comp.HumStream == null)
            return;

        _audio.Stop(comp.HumStream);
        comp.HumStream = null;
    }

    private void SetPhase(EntityUid uid, FSSingularityComponent comp, FSSingularityPhase phase)
    {
        comp.Phase = phase;
        Dirty(uid, comp);
        _appearance.SetData(uid, FSSingularityVisuals.Phase, phase);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSSingularityComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            comp.Elapsed += frameTime;

            var pullEnds = comp.StartDuration + comp.LoopDuration;

            if (comp.Phase == FSSingularityPhase.Start && comp.Elapsed >= comp.StartDuration)
                SetPhase(uid, comp, FSSingularityPhase.Loop);

            if (comp.Phase == FSSingularityPhase.Loop && comp.Elapsed >= pullEnds)
            {
                SetPhase(uid, comp, FSSingularityPhase.End);
                StopHum(comp);
            }

            if (comp.Elapsed >= pullEnds + comp.EndDuration)
            {
                QueueDel(uid);
                continue;
            }

            if (comp.Phase == FSSingularityPhase.End)
                continue;

            var damageThisTick = false;
            comp.DamageAccumulator += frameTime;
            if (comp.DamageAccumulator >= DamageInterval)
            {
                comp.DamageAccumulator -= DamageInterval;
                damageThisTick = true;
            }

            Pull(uid, comp, xform, damageThisTick);
        }
    }

    private void Pull(EntityUid uid, FSSingularityComponent comp, TransformComponent xform, bool damage)
    {
        var origin = comp.Thrower is { } thrower && !TerminatingOrDeleted(thrower) ? thrower : uid;
        var centre = _transform.GetWorldPosition(xform);
        var targets = _targetSetPool.Get();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(
            new MapCoordinates(centre, xform.MapID), comp.Radius, targets);

        foreach (var (targetUid, _) in targets)
        {
            if (TryComp<MobStateComponent>(targetUid, out var mobState)
                && mobState.CurrentState == MobState.Dead)
                continue;

            if (damage)
                _damageable.TryChangeDamage(targetUid, comp.DamagePerSecond * DamageInterval, ignoreResistances: false, origin: origin);

            if (!_physicsQuery.TryGetComponent(targetUid, out var body))
                continue;

            var toCentre = centre - _transform.GetWorldPosition(targetUid);
            var dist = toCentre.Length();
            if (dist < 0.1f)
                continue;

            var falloff = 1f - dist / comp.Radius;
            var pull = toCentre / dist * (comp.PullStrength * falloff);
            _physics.SetLinearVelocity(targetUid, body.LinearVelocity + pull, body: body);
        }

        _targetSetPool.Return(targets);
    }
}
