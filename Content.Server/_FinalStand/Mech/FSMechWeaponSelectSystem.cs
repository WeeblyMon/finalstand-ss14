using Content.Shared._FinalStand.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._FinalStand.Mech;

public sealed partial class FSMechWeaponSelectSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier SelectSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSMechSelectWeaponEvent>(OnSelect);
    }

    private void OnSelect(FSMechSelectWeaponEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } pilot
            || !TryComp<VehicleOperatorComponent>(pilot, out var op)
            || op.Vehicle is not { } vehicle
            || !TryComp<MechComponent>(vehicle, out var mech)
            || mech.Broken)
            return;

        var weapon = GetEntity(ev.Weapon);
        if (!mech.EquipmentContainer.Contains(weapon) || mech.CurrentSelectedEquipment == weapon)
            return;

        mech.CurrentSelectedEquipment = weapon;
        Dirty(vehicle, mech);
        _audio.PlayPvs(SelectSound, vehicle);
    }
}
