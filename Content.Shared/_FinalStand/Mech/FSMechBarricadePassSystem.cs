using Content.Shared._FinalStand.Repair;
using Robust.Shared.Physics.Events;

namespace Content.Shared._FinalStand.Mech;

public sealed class FSMechBarricadePassSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSCombatMechComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<FSCombatMechComponent> ent, ref PreventCollideEvent args)
    {
        if (HasComp<FSPlateRepairComponent>(args.OtherEntity))
            args.Cancelled = true;
    }
}
