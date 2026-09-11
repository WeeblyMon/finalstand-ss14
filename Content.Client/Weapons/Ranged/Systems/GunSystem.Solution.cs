using Content.Shared.Weapons.Ranged.Components;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
    // Every other ammo provider has a counter handler. SolutionAmmoProvider never got one, because
    // the only vanilla user is the water gun, which carries no AmmoCounter. A magazine-fed gun
    // forwards the counter event to its magazine, so without this the display stays blank.
    protected override void InitializeSolution()
    {
        base.InitializeSolution();
        SubscribeLocalEvent<SolutionAmmoProviderComponent, UpdateAmmoCounterEvent>(OnSolutionAmmoCounter);
    }

    private void OnSolutionAmmoCounter(Entity<SolutionAmmoProviderComponent> ent, ref UpdateAmmoCounterEvent args)
    {
        if (args.Control is DefaultStatusControl control)
            control.Update(ent.Comp.Shots, ent.Comp.MaxShots);
    }
}
