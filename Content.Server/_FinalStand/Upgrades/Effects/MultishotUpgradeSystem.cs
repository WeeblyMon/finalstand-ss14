using Content.Shared._FinalStand.Shop;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Upgrades.Effects;

// chance per shot to fire 1-3 extra pellets fanned within the gun's spread cone (via PelletCountUpgradeSystem)
public sealed class MultishotUpgradeSystem : EntitySystem
{
    [Dependency] private readonly PelletCountUpgradeSystem _pelletCount = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const float ExtraRollChance = 0.33f;
    private const int MaxExtra = 3;

    public void HandleAmmoShot(EntityUid uid, FSWeaponUpgradeStateComponent state, AmmoShotEvent args)
    {
        if (state.MultishotLevel <= 0 || args.FiredProjectiles.Count == 0)
            return;

        if (!_random.Prob(state.MultishotLevel * 0.2f))
            return;

        var extraCount = 1;
        for (var i = 1; i < state.MultishotLevel && extraCount < MaxExtra; i++)
        {
            if (_random.Prob(ExtraRollChance))
                extraCount++;
        }

        _pelletCount.SpawnExtraPellets(uid, args.FiredProjectiles, extraCount, addPierce: false);
    }
}
