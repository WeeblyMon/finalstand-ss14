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

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSFireProjectileComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var xform = Transform(uid);
            var worldPos = _transform.GetWorldPosition(uid);
            var candidates = new HashSet<Entity<FlammableComponent>>();
            _lookup.GetEntitiesInRange<FlammableComponent>(new MapCoordinates(worldPos, xform.MapID), HitRadius, candidates);

            foreach (var (targetUid, flammable) in candidates)
            {
                if (HasComp<WaveSpawnedTagComponent>(targetUid)) continue;
                if (!comp.AlreadyIgnited.Add(targetUid)) continue;
                flammable.FireStacks += 0.1f;
                _flammable.Ignite(targetUid, uid, flammable);
            }
        }
    }
}
