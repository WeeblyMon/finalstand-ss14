using Content.Shared._FinalStand.Grenades;
using Content.Shared.Trigger;
using Robust.Shared.Spawners;

namespace Content.Server._FinalStand.Grenades;

public sealed class FSBaitOnTriggerSystem : XOnTriggerSystem<FSBaitOnTriggerComponent>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    protected override void OnTrigger(Entity<FSBaitOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        var coords = _transform.GetMapCoordinates(ent.Owner);
        var decoy = Spawn(ent.Comp.BaitProtoId, coords);
        var tds = EnsureComp<TimedDespawnComponent>(decoy);
        tds.Lifetime = ent.Comp.BaitDuration;
        args.Handled = true;
    }
}
