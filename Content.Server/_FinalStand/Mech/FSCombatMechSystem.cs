using Content.Server.Destructible;
using Content.Server.Mech.Systems;
using Content.Server._FinalStand.Effects;
using Content.Shared._FinalStand.HMG;
using Content.Shared._FinalStand.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Content.Shared.Vehicle.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared._FinalStand.Particles;

namespace Content.Server._FinalStand.Mech;

// A wrecked mech unplants and takes no more damage, so it stays until repaired.
public sealed partial class FSCombatMechSystem : EntitySystem
{
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private MechSystem _mech = default!;
    [Dependency] private FSMechBailoutSystem _bailout = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSFlairSystem _flair = default!;
    [Dependency] private FSSiegeModeSystem _siege = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedContentEyeSystem _eye = default!;

    private static readonly ProtoId<FSParticleEmitterPrototype> DustPuff = "FSDustPuff";
    private static readonly ProtoId<FSParticleEmitterPrototype> SmokeBurst = "FSSmokeBurst";
    private static readonly ProtoId<FSParticleEmitterPrototype> StompRing = "FSStompRing";
    private static readonly ProtoId<FSParticleEmitterPrototype> WeldSparks = "FSWeldSparks";

    private static readonly TimeSpan BailoutTime = TimeSpan.FromSeconds(3);
    private static readonly SoundSpecifier WreckSound = new SoundCollectionSpecifier("MetalBreak");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSCombatMechComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FSCombatMechComponent, VehicleOperatorSetEvent>(OnOperatorSet);
        SubscribeLocalEvent<FSCombatMechComponent, DamageChangedEvent>(OnDamageChanged, after: [typeof(MechSystem)]);
        SubscribeLocalEvent<FSCombatMechComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<VehicleOperatorComponent, BeforeDamageChangedEvent>(OnPilotDamage);
        SubscribeLocalEvent<FSSiegeModeComponent, FSSiegePlantedEvent>(OnPlanted);
        SubscribeLocalEvent<MechEquipmentComponent, FSMechGunChargeEvent>(OnCharge);
        SubscribeLocalEvent<MechEquipmentComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnMapInit(Entity<FSCombatMechComponent> ent, ref MapInitEvent args)
    {
        RemComp<DestructibleComponent>(ent);
    }

    private void OnOperatorSet(Entity<FSCombatMechComponent> ent, ref VehicleOperatorSetEvent args)
    {
        if (args.OldOperator is { } oldPilot)
        {
            _faction.ClearFactions((ent.Owner, null));
            if (ent.Comp.PilotMaxZoom is { } maxZoom && HasComp<ContentEyeComponent>(oldPilot))
            {
                _eye.SetMaxZoom(oldPilot, maxZoom);
                _eye.ResetZoom(oldPilot);
            }

            ent.Comp.PilotMaxZoom = null;
        }

        if (args.NewOperator is { } newPilot && TryComp<ContentEyeComponent>(newPilot, out var eye))
        {
            ent.Comp.PilotMaxZoom = eye.MaxZoom;
            _eye.SetMaxZoom(newPilot, ent.Comp.PilotZoom, eye);
            _eye.SetZoom(newPilot, ent.Comp.PilotZoom, eye: eye);
        }

        // The open-cockpit sprite only faces south, so an empty mech faces south too and its guns match it.
        if (args.NewOperator is not { } pilot)
        {
            _transform.SetLocalRotation(ent.Owner, Angle.Zero);
            return;
        }

        ent.Comp.LastPilot = pilot;
        if (!TryComp<NpcFactionMemberComponent>(pilot, out var pilotFactions))
            return;

        _faction.ClearFactions((ent.Owner, null), false);
        _faction.AddFactions((ent.Owner, null), pilotFactions.Factions);
    }

    private void OnDamageChanged(Entity<FSCombatMechComponent> ent, ref DamageChangedEvent args)
    {
        if (!TryComp<MechComponent>(ent, out var mech))
            return;

        if (!mech.Broken)
        {
            ent.Comp.WasBroken = false;
            return;
        }

        if (ent.Comp.WasBroken)
            return;

        ent.Comp.WasBroken = true;
        if (TryComp<FSSiegeModeComponent>(ent, out var siege) && siege.Planted)
            _siege.SetPlanted((ent.Owner, siege), false);

        _audio.PlayPvs(WreckSound, ent);
        _flair.Burst(SmokeBurst, ent, kick: 0.8f);
        _flair.Burst(WeldSparks, ent);
        if (ent.Comp.LastPilot is not { } pilot || TerminatingOrDeleted(pilot) || !_transform.InRange(pilot, ent.Owner, 2f))
            return;

        _bailout.Start(pilot, BailoutTime);
        _popup.PopupEntity(Loc.GetString("fs-mech-wrecked"), pilot, pilot, PopupType.LargeCaution);
    }

    private void OnBeforeDamage(Entity<FSCombatMechComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (TryComp<MechComponent>(ent, out var mech) && mech.Broken && args.Damage.AnyPositive())
            args.Cancelled = true;
    }

    private void OnPilotDamage(Entity<VehicleOperatorComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Cancelled
            || !args.Damage.AnyPositive()
            || args.Origin is not { } origin
            || origin == ent.Owner
            || ent.Comp.Vehicle is not { } vehicle
            || !HasComp<FSCombatMechComponent>(vehicle)
            || !TryComp<MechComponent>(vehicle, out var mech)
            || mech.Broken)
            return;

        args.Cancelled = true;
        _damageable.TryChangeDamage(vehicle, args.Damage, origin: origin);
    }

    private void OnPlanted(Entity<FSSiegeModeComponent> ent, ref FSSiegePlantedEvent args)
    {
        if (!args.Planted)
            return;

        _flair.Burst(StompRing, ent, kick: 0.7f);
        _flair.Burst(DustPuff, ent);
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
