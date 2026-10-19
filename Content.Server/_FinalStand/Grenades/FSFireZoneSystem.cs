using Content.Server._FinalStand.Spawners;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._FinalStand.Grenades;
using Content.Shared.Atmos.Components;
using Content.Shared.Trigger;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Server._FinalStand.Grenades;

public sealed partial class FSFireZoneSystem : EntitySystem
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    // Elapsed time is accumulated and passed through, so throttling does not change how fast
    // a zone builds fire stacks.
    private const float TickInterval = 0.25f;

    private EntityQuery<WaveSpawnedTagComponent> _waveQuery;

    private readonly ObjectPool<HashSet<Entity<FlammableComponent>>> _flammableSetPool =
        new DefaultObjectPool<HashSet<Entity<FlammableComponent>>>(
            new SetPolicy<Entity<FlammableComponent>>());

    public override void Initialize()
    {
        base.Initialize();
        _waveQuery = GetEntityQuery<WaveSpawnedTagComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSFireZoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var zone, out var xform))
        {
            zone.Accumulator += frameTime;
            if (zone.Accumulator < TickInterval)
                continue;

            var elapsed = zone.Accumulator;
            zone.Accumulator = 0f;

            var worldPos = _transform.GetWorldPosition(uid);
            var candidates = _flammableSetPool.Get();
            _lookup.GetEntitiesInRange<FlammableComponent>(
                new MapCoordinates(worldPos, xform.MapID),
                zone.Radius,
                candidates);

            foreach (var (targetUid, flammable) in candidates)
            {
                // Only the horde burns. Skipping actors spared live players but not disconnected ones,
                // allied mobs or flammable defences.
                if (!_waveQuery.HasComp(targetUid))
                    continue;

                flammable.FireStacks += zone.IgniteStacksPerSecond * elapsed;
                _flammable.Ignite(targetUid, uid, flammable);
            }

            _flammableSetPool.Return(candidates);
        }
    }
}

public sealed partial class FSFireZoneOnTriggerSystem : XOnTriggerSystem<FSFireZoneOnTriggerComponent>
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const int MaxFlameExtent = 4;

    protected override void OnTrigger(Entity<FSFireZoneOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        var coords = _transform.GetMapCoordinates(ent.Owner);
        var fireZone = Spawn(ent.Comp.FireZoneProtoId, coords);
        var tds = EnsureComp<TimedDespawnComponent>(fireZone);
        tds.Lifetime = ent.Comp.BurnDuration;
        if (TryComp<FSFireZoneComponent>(fireZone, out var fz))
        {
            if (ent.Comp.EffectRadius > 0f)
                fz.Radius += ent.Comp.EffectRadius;
            if (ent.Comp.DamageMultiplier != 1f)
                fz.IgniteStacksPerSecond *= ent.Comp.DamageMultiplier;
            Dirty(fireZone, fz);
            SpawnFlames(fireZone, fz, ent.Comp.BurnDuration);
        }
        args.Handled = true;
    }

    private void SpawnFlames(EntityUid zone, FSFireZoneComponent zoneComp, float lifetime)
    {
        var origin = Transform(zone).Coordinates;
        var extent = Math.Min((int)MathF.Ceiling(zoneComp.Radius), MaxFlameExtent);

        for (var x = -extent; x <= extent; x++)
        {
            for (var y = -extent; y <= extent; y++)
            {
                var offset = new Vector2(x, y);
                if (offset.Length() > zoneComp.Radius)
                    continue;

                var jitter = new Vector2(_random.NextFloat(-0.25f, 0.25f), _random.NextFloat(-0.25f, 0.25f));
                var flame = Spawn(zoneComp.FlameProtoId, origin.Offset(offset + jitter));
                EnsureComp<TimedDespawnComponent>(flame).Lifetime = lifetime;
            }
        }
    }
}
