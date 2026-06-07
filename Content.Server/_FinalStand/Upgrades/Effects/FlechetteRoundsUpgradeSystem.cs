using Content.Shared._FinalStand.Shop;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._FinalStand.Upgrades.Effects;

// spawns the extra flechette pellets; pierce on originals is set in FSWeaponUpgradeRuntimeSystem
public sealed class FlechetteRoundsUpgradeSystem : EntitySystem
{
    [Dependency] private readonly PelletCountUpgradeSystem _pellets = default!;

    public void HandleAmmoShot(EntityUid uid, FSWeaponUpgradeStateComponent state, AmmoShotEvent args)
    {
        if (!state.FlechetteEnabled || args.FiredProjectiles.Count == 0)
            return;

        // Spawn one extra per original pellet — doubling the total count, all with pierce-1.
        _pellets.SpawnExtraPellets(uid, args.FiredProjectiles, args.FiredProjectiles.Count, addPierce: true);
    }
}
