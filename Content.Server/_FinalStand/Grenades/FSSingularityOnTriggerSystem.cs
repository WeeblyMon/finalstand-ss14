using Content.Shared._FinalStand.Grenades;
using Content.Shared.Trigger;

namespace Content.Server._FinalStand.Grenades;

public sealed partial class FSSingularityOnTriggerSystem : XOnTriggerSystem<FSSingularityOnTriggerComponent>
{
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override void OnTrigger(Entity<FSSingularityOnTriggerComponent> ent, EntityUid target, ref TriggerEvent args)
    {
        var coords = _transform.GetMapCoordinates(ent.Owner);
        var singularity = Spawn(ent.Comp.SingularityProtoId, coords);

        if (TryComp<FSSingularityComponent>(singularity, out var sing))
        {
            // An impact fuse triggers with whatever it hit as the user, so the stored thrower wins.
            sing.Thrower = TryComp<FSGrenadeOwnerComponent>(ent, out var owner) && owner.Thrower.IsValid()
                ? owner.Thrower
                : args.User;
            sing.Radius += ent.Comp.ExtraRadius;
            sing.LoopDuration += ent.Comp.ExtraDuration;
            sing.DamagePerSecond *= ent.Comp.DamageMultiplier;
            Dirty(singularity, sing);
        }

        args.Handled = true;
    }
}
