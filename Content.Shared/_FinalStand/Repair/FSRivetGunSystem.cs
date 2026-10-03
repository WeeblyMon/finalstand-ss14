// The rivet gun feeds itself a sheet at a time from the steel its holder is carrying.
using Content.Shared._FinalStand.Materials;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._FinalStand.Repair;

public sealed class FSRivetGunSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private FSCarriedMaterialSystem _materials = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRivetGunComponent, AttemptShootEvent>(OnAttemptShoot);
    }

    private void OnAttemptShoot(Entity<FSRivetGunComponent> ent, ref AttemptShootEvent args)
    {
        if (args.Cancelled
            || !TryComp<BasicEntityAmmoProviderComponent>(ent, out var ammo)
            || ammo.Count is not 0)
            return;

        if (!_materials.TryConsume(args.User, ent.Comp.Material, 1))
        {
            args.Cancelled = true;
            args.Message = Loc.GetString("fs-rivet-gun-no-steel");
            return;
        }

        _gun.ChangeBasicEntityAmmoCount((ent, ammo), ent.Comp.RivetsPerSheet);
    }
}
