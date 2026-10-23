using Content.Shared.Mech.Components;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Mech;

public sealed partial class FSMechWeaponSelectSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly SoundSpecifier SelectSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<FSMechSelectWeaponEvent>(OnSelect);
        SubscribeLocalEvent<FSMechWeaponTogglesComponent, EntRemovedFromContainerMessage>(OnRemoved);
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

    // A dismounted or deleted weapon mustn't linger in the networked set. Not while applying state: a PVS detach
    // takes it out of the container on the client too, and the set would drift from the server's.
    private void OnRemoved(Entity<FSMechWeaponTogglesComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (_timing.ApplyingState)
            return;

        if (ent.Comp.Disabled.Remove(args.Entity))
            Dirty(ent);
    }

    public bool IsArmed(Entity<FSMechWeaponTogglesComponent?> mech, EntityUid weapon)
    {
        return !Resolve(mech, ref mech.Comp, false) || !mech.Comp.Disabled.Contains(weapon);
    }

    public bool TryGetArmedGuns(EntityUid pilot, List<Entity<GunComponent>> guns)
    {
        guns.Clear();
        if (!TryGetPilotMech(pilot, out var vehicle, out var mech))
            return false;

        TryComp<FSMechWeaponTogglesComponent>(vehicle, out var toggles);
        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (IsArmed((vehicle, toggles), equipment) && TryComp<GunComponent>(equipment, out var gun))
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

    private static EntityUid? FirstArmed(MechComponent mech, FSMechWeaponTogglesComponent toggles)
    {
        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (!toggles.Disabled.Contains(equipment))
                return equipment;
        }

        return null;
    }
}
