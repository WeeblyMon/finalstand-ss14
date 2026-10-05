// Mech guns only fire while mounted in a mech, and refill their cell from the mech's own power before each shot.
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Shared._FinalStand.Mech;

public sealed partial class FSMechGunSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechEquipmentComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<MechEquipmentComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.EquipmentOwner is not { } mech || !HasComp<MechComponent>(mech))
        {
            args.Cancel();
            return;
        }

        var ev = new FSMechGunChargeEvent(mech);
        RaiseLocalEvent(ent, ref ev);
    }
}

[ByRefEvent]
public readonly record struct FSMechGunChargeEvent(EntityUid Mech);
