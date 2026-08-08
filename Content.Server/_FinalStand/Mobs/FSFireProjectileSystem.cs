using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
using Content.Server._FinalStand.Spawners;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Atmos.Components;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSFireProjectileSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float HitRadius = 0.5f;

    // A particle covers HitRadius every 0.05s, so this cannot step over a target.
    private const float CheckInterval = 0.05f;

    private readonly ObjectPool<HashSet<Entity<FlammableComponent>>> _flammableSetPool =
        new DefaultObjectPool<HashSet<Entity<FlammableComponent>>>(
            new SetPolicy<Entity<FlammableComponent>>());

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSFireProjectileComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.CheckAccumulator += frameTime;
            if (comp.CheckAccumulator < CheckInterval)
                continue;
            comp.CheckAccumulator = 0f;

            var xform = Transform(uid);
            var worldPos = _transform.GetWorldPosition(uid);
            var candidates = _flammableSetPool.Get();
            _lookup.GetEntitiesInRange<FlammableComponent>(new MapCoordinates(worldPos, xform.MapID), HitRadius, candidates);

            foreach (var (targetUid, flammable) in candidates)
            {
                if (HasComp<WaveSpawnedTagComponent>(targetUid)) continue;
                if (!comp.AlreadyIgnited.Add(targetUid)) continue;
                flammable.FireStacks += 0.1f;
                _flammable.Ignite(targetUid, uid, flammable);
            }
            _flammableSetPool.Return(candidates);
        }
    }
}
