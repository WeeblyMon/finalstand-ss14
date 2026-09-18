using Content.Shared.Weapons.Ranged.Components;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class GunSystem
{
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
