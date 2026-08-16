using Content.Server._FinalStand.Spawners;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server._FinalStand.Deployables;

public abstract class FSDeployableAuraSystem<TAura> : EntitySystem where TAura : IComponent
{
    [Dependency] protected EntityLookupSystem Lookup = default!;
    [Dependency] protected SharedTransformSystem XformSystem = default!;

    protected const float TickInterval = 0.25f;
    protected const float RefreshDuration = TickInterval * 3f;

    private readonly ObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>> _enemyPool =
        new DefaultObjectPool<HashSet<Entity<WaveSpawnedTagComponent>>>(
            new SetPolicy<Entity<WaveSpawnedTagComponent>>());

    private float _accumulator;

    protected abstract float GetRadius(TAura aura);

    protected abstract void ApplyTo(EntityUid target, EntityUid source, TAura aura);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;
        if (_accumulator < TickInterval)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<TAura, TransformComponent>();
        while (query.MoveNext(out var uid, out var aura, out var xform))
        {
            var candidates = _enemyPool.Get();
            Lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(
                new MapCoordinates(XformSystem.GetWorldPosition(uid), xform.MapID),
                GetRadius(aura),
                candidates);

            foreach (var (targetUid, _) in candidates)
                ApplyTo(targetUid, uid, aura);

            _enemyPool.Return(candidates);
        }
    }
}
