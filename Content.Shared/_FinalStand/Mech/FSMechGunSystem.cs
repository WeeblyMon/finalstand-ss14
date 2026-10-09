// Mech guns only fire while mounted in a mech, and refill their cell from the mech's own power before each shot.
// The mech is found through the container the gun sits in, which clients know too, so the pilot's shots are predicted
// (and heard) locally instead of being cancelled on their side.
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Containers;

namespace Content.Shared._FinalStand.Mech;

public sealed partial class FSMechGunSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MechEquipmentComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<MechEquipmentComponent> ent, ref ShotAttemptedEvent args)
    {
        if (!_container.TryGetContainingContainer(ent.Owner, out var container) || !HasComp<MechComponent>(container.Owner))
        {
            args.Cancel();
            return;
        }

        var ev = new FSMechGunChargeEvent(container.Owner);
        RaiseLocalEvent(ent, ref ev);
    }
}

[ByRefEvent]
public readonly record struct FSMechGunChargeEvent(EntityUid Mech);
