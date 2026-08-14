using Content.Shared._FinalStand.Grenades;
using Content.Shared.Throwing;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Grenades;

public sealed partial class FSClusterOnTriggerSystem : XOnTriggerSystem<FSClusterOnTriggerComponent>
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TriggerSystem _trigger = default!;

    protected override void OnTrigger(Entity<FSClusterOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        var coords = _transform.GetMapCoordinates(ent.Owner);
        var count = ent.Comp.Count;
        var segmentAngle = 360f / count;

        TryComp<FSGrenadeOwnerComponent>(ent.Owner, out var ownerComp);

        for (var i = 0; i < count; i++)
        {
            var sub = Spawn(ent.Comp.SubGrenadeProtoId, coords);

            if (ownerComp != null && ownerComp.Thrower.IsValid())
                EnsureComp<FSGrenadeOwnerComponent>(sub).Thrower = ownerComp.Thrower;

            _trigger.ActivateTimerTrigger(sub);
            var degrees = segmentAngle * i + _random.NextFloat(0f, segmentAngle * 0.4f);
            var dir = Angle.FromDegrees(degrees).ToVec() * ent.Comp.Distance;
            _throwing.TryThrow(sub, dir, ent.Comp.Velocity);
        }

        args.Handled = true;
    }
}
