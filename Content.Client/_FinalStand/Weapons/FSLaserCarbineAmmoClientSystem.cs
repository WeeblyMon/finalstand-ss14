using Content.Client.Items;
using Content.Client.Weapons.Ranged.Systems;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.Weapons;

public sealed class FSLaserCarbineAmmoClientSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        Subs.ItemStatus<FSLaserCarbineAmmoComponent>(ent => new FSLaserCarbineAmmoStatusControl(ent));

        // suppress the vanilla battery pip/box control (GunSystem.OnControl) so only our numeric
        // readout shows - must run after GunSystem's own handler to win the ev.Control assignment
        SubscribeLocalEvent<FSLaserCarbineAmmoComponent, GunSystem.AmmoCounterControlEvent>(OnGetControl,
            after: new[] { typeof(GunSystem) });
    }

    private void OnGetControl(EntityUid uid, FSLaserCarbineAmmoComponent comp, GunSystem.AmmoCounterControlEvent args)
    {
        args.Control = new Control { Visible = false };
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<FSLaserCarbineAmmoComponent, BatteryAmmoProviderComponent>();
        while (query.MoveNext(out _, out var carbine, out var ammo))
        {
            carbine.CurrentAmmo = ammo.Shots;
            carbine.MaxAmmo = ammo.Capacity;
        }
    }
}
