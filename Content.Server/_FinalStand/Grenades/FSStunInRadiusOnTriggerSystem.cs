using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Grenades;
using Content.Shared.Damage.Components;
using Content.Shared.Trigger;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Grenades;

public sealed partial class FSStunInRadiusOnTriggerSystem : XOnTriggerSystem<FSStunInRadiusOnTriggerComponent>
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FSStunOverrideSystem _fsStun = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private EntityQuery<FSFriendlyFireComponent> _ffQuery;

    public override void Initialize()
    {
        base.Initialize();
        _ffQuery = GetEntityQuery<FSFriendlyFireComponent>();
    }

    protected override void OnTrigger(Entity<FSStunInRadiusOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        var worldPos = _transform.GetWorldPosition(ent.Owner);
        var xform = Transform(ent.Owner);

        var candidates = new HashSet<Entity<DamageableComponent>>();
        _lookup.GetEntitiesInRange<DamageableComponent>(
            new MapCoordinates(worldPos, xform.MapID),
            ent.Comp.Radius,
            candidates);

        foreach (var (targetUid, _) in candidates)
        {
            if (_ffQuery.HasComponent(targetUid))
                continue;
            _fsStun.TryForceStun(targetUid, ent.Comp.StunDuration);
        }

        args.Handled = true;
    }
}
