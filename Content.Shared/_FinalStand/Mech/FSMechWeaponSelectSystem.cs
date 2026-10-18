using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._FinalStand.Mech;

public sealed partial class FSMechWeaponSelectSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier SelectSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<FSMechSelectWeaponEvent>(OnSelect);
    }

    private void OnSelect(FSMechSelectWeaponEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } pilot
            || !TryGetPilotMech(pilot, out var vehicle, out var mech))
            return;

        var weapon = GetEntity(ev.Weapon);
        if (!mech.EquipmentContainer.Contains(weapon))
            return;

        var toggles = EnsureComp<FSMechWeaponTogglesComponent>(vehicle);
        if (toggles.Disabled.Remove(weapon))
        {
            mech.CurrentSelectedEquipment = weapon;
        }
        else
        {
            toggles.Disabled.Add(weapon);
            if (mech.CurrentSelectedEquipment == weapon)
                mech.CurrentSelectedEquipment = FirstArmed(mech, toggles);
        }

        Dirty(vehicle, toggles);
        Dirty(vehicle, mech);
        _audio.PlayPredicted(SelectSound, vehicle, pilot);
    }

    public bool IsArmed(EntityUid mech, EntityUid weapon)
    {
        return !TryComp<FSMechWeaponTogglesComponent>(mech, out var toggles) || !toggles.Disabled.Contains(weapon);
    }

    public bool TryGetArmedGuns(EntityUid pilot, List<Entity<GunComponent>> guns)
    {
        guns.Clear();
        if (!TryGetPilotMech(pilot, out var vehicle, out var mech))
            return false;

        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (IsArmed(vehicle, equipment) && TryComp<GunComponent>(equipment, out var gun))
                guns.Add((equipment, gun));
        }

        return true;
    }

    public bool TryGetPilotGun(EntityUid pilot, EntityUid weapon, out Entity<GunComponent> gun)
    {
        gun = default;
        if (!TryGetPilotMech(pilot, out var vehicle, out var mech)
            || !mech.EquipmentContainer.Contains(weapon)
            || !IsArmed(vehicle, weapon)
            || !TryComp<GunComponent>(weapon, out var gunComp))
            return false;

        gun = (weapon, gunComp);
        return true;
    }

    private bool TryGetPilotMech(EntityUid pilot, out EntityUid vehicle, out MechComponent mech)
    {
        vehicle = default;
        mech = default!;
        if (!TryComp<VehicleOperatorComponent>(pilot, out var op)
            || op.Vehicle is not { } found
            || !TryComp<MechComponent>(found, out var mechComp)
            || mechComp.Broken)
            return false;

        vehicle = found;
        mech = mechComp;
        return true;
    }

    private EntityUid? FirstArmed(MechComponent mech, FSMechWeaponTogglesComponent toggles)
    {
        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (!toggles.Disabled.Contains(equipment))
                return equipment;
        }

        return null;
    }
}
