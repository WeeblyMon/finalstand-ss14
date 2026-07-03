using Content.Server.Atmos.EntitySystems;
using Content.Shared._FinalStand.Grenades;
using Content.Shared.Atmos.Components;
using Content.Shared.Trigger;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Grenades;

public sealed class FSFireZoneSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSFireZoneComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var zone, out var xform))
        {
            var worldPos = _transform.GetWorldPosition(uid);
            var candidates = new HashSet<Entity<FlammableComponent>>();
            _lookup.GetEntitiesInRange<FlammableComponent>(
                new MapCoordinates(worldPos, xform.MapID),
                zone.Radius,
                candidates);

            foreach (var (targetUid, flammable) in candidates)
            {
                if (HasComp<ActorComponent>(targetUid))
                    continue;
                flammable.FireStacks += zone.IgniteStacksPerSecond * frameTime;
                _flammable.Ignite(targetUid, uid, flammable);
            }
        }
    }
}

public sealed class FSFireZoneOnTriggerSystem : XOnTriggerSystem<FSFireZoneOnTriggerComponent>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

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
        }
        args.Handled = true;
    }
}
