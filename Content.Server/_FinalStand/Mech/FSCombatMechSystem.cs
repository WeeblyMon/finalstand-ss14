// Combat mechs on the server: the pilot's factions ride with the mech so zombies go for it, the pilot sees a little further,
// guns refill from mech power, and rounds fired from a mech don't hit it and pay a flat amount.
using Content.Server.Mech.Systems;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Projectiles;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._FinalStand.Mech;

public sealed partial class FSCombatMechSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedContentEyeSystem _eye = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private MechSystem _mech = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSCombatMechComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<MechEquipmentComponent, FSMechGunChargeEvent>(OnCharge);
        SubscribeLocalEvent<MechEquipmentComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnOperatorSet(Entity<FSCombatMechComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator is { } oldPilot)
        {
            _faction.ClearFactions((ent.Owner, null));
            _eye.ResetZoom(oldPilot);
        }

        if (args.NewOperator is not { } pilot)
            return;

        if (TryComp<NpcFactionMemberComponent>(pilot, out var pilotFactions))
        {
            _faction.ClearFactions((ent.Owner, null), false);
            _faction.AddFactions((ent.Owner, null), pilotFactions.Factions);
        }

        _eye.SetZoom(pilot, ent.Comp.PilotZoom, ignoreLimits: true);
    }

    private void OnCharge(Entity<MechEquipmentComponent> ent, ref FSMechGunChargeEvent args)
    {
        if (!TryComp<BatteryComponent>(ent, out var battery) || !TryComp<MechComponent>(args.Mech, out var mech))
            return;

        var missing = battery.MaxCharge - _battery.GetCharge((ent.Owner, battery));
        if (missing <= 0f || mech.Energy < missing || !_mech.TryChangeEnergy(args.Mech, -missing, mech))
            return;

        _battery.SetCharge((ent.Owner, battery), battery.MaxCharge);
    }

    private void OnAmmoShot(Entity<MechEquipmentComponent> ent, ref AmmoShotEvent args)
    {
        if (ent.Comp.EquipmentOwner is not { } mech || !TryComp<FSCombatMechComponent>(mech, out var combat))
            return;

        foreach (var shot in args.FiredProjectiles)
        {
            if (TryComp<ProjectileComponent>(shot, out var projectile))
                projectile.Weapon = mech;

            EnsureComp<FSFlatMoneyProjectileComponent>(shot).Amount = combat.MoneyPerHit;
        }
    }
}
