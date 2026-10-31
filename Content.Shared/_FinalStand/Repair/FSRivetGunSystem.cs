using Content.Shared._FinalStand.Materials;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._FinalStand.Repair;

// The rivet gun feeds itself a sheet at a time from the steel its holder carries.
public sealed partial class FSRivetGunSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private FSCarriedMaterialSystem _materials = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRivetGunComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<FSRivetGunComponent, GunRefreshModifiersEvent>(OnRefreshModifiers);
    }

    public void SetResearchBonuses(Entity<FSRivetGunComponent> ent, float fireRateMultiplier, float repairCap)
    {
        if (ent.Comp.FireRateMultiplier.Equals(fireRateMultiplier) && ent.Comp.RepairCap.Equals(repairCap))
            return;

        ent.Comp.RepairCap = repairCap;
        if (!ent.Comp.FireRateMultiplier.Equals(fireRateMultiplier))
        {
            ent.Comp.FireRateMultiplier = fireRateMultiplier;
            _gun.RefreshModifiers(ent.Owner);
        }

        Dirty(ent);
    }

    private void OnRefreshModifiers(Entity<FSRivetGunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        args.FireRate *= ent.Comp.FireRateMultiplier;
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
