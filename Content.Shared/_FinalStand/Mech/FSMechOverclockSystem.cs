using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Mech;

// Fits to the gun in hand, or to a mounted gun when used on its mech.
public sealed partial class FSMechOverclockSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier FitSound = new SoundPathSpecifier("/Audio/Items/ratchet.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMechOverclockComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<FSMechOverclockedComponent, GunRefreshModifiersEvent>(OnRefresh);
        SubscribeLocalEvent<FSMechOverclockedComponent, AmmoShotEvent>(OnShot);
        SubscribeLocalEvent<FSMechOverclockedComponent, ExaminedEvent>(OnExamined);
    }

    private void OnAfterInteract(Entity<FSMechOverclockComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = true;
        if (FindGun(ent.Comp, target) is not { } gun)
        {
            _popup.PopupClient(Loc.GetString("fs-overclock-wrong-gun", ("gun", GunName(ent.Comp))), target, args.User);
            return;
        }

        if (HasComp<FSMechOverclockedComponent>(gun))
        {
            _popup.PopupClient(Loc.GetString("fs-overclock-already"), target, args.User);
            return;
        }

        if (_net.IsClient)
            return;

        var overclocked = EnsureComp<FSMechOverclockedComponent>(gun);
        overclocked.Stats = ent.Comp.Stats;
        Dirty(gun, overclocked);
        _gun.RefreshModifiers(gun);
        _audio.PlayPvs(FitSound, target);
        _popup.PopupEntity(Loc.GetString("fs-overclock-fitted", ("name", Loc.GetString(ent.Comp.Stats.Name))), target, args.User, PopupType.Medium);
        QueueDel(ent);
    }

    private EntityUid? FindGun(FSMechOverclockComponent overclock, EntityUid target)
    {
        if (MetaData(target).EntityPrototype?.ID == overclock.Gun.Id)
            return target;

        if (!TryComp<MechComponent>(target, out var mech))
            return null;

        foreach (var equipment in mech.EquipmentContainer.ContainedEntities)
        {
            if (MetaData(equipment).EntityPrototype?.ID == overclock.Gun.Id)
                return equipment;
        }

        return null;
    }

    private string GunName(FSMechOverclockComponent overclock)
    {
        return _proto.TryIndex(overclock.Gun, out var proto) ? proto.Name : overclock.Gun.Id;
    }

    private void OnRefresh(Entity<FSMechOverclockedComponent> ent, ref GunRefreshModifiersEvent args)
    {
        var stats = ent.Comp.Stats;
        args.FireRate *= stats.FireRate;
        args.ProjectileSpeed *= stats.ProjectileSpeed;
        args.ShotsPerBurst += stats.BurstBonus;
        args.MinAngle *= stats.Spread;
        args.MaxAngle *= stats.Spread;
        args.AngleIncrease *= stats.Spread;
    }

    private void OnShot(Entity<FSMechOverclockedComponent> ent, ref AmmoShotEvent args)
    {
        if (MathHelper.CloseTo(ent.Comp.Stats.Damage, 1f))
            return;

        foreach (var projectile in args.FiredProjectiles)
        {
            if (TryComp<ProjectileComponent>(projectile, out var comp))
                comp.Damage *= ent.Comp.Stats.Damage;
        }
    }

    private void OnExamined(Entity<FSMechOverclockedComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fs-overclock-examine", ("name", Loc.GetString(ent.Comp.Stats.Name))));
    }
}
