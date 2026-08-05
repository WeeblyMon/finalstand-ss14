// Part of FSPlayerUpgradesSystem: upgrades that hand the player an item.
using Content.Shared._FinalStand.Shop;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSPlayerUpgradesSystem
{
    private bool TryApplyPayload(EntityUid weapon, EntityUid player, WeaponUpgradeDef def,
        int newLevel, bool spawnItems, FSWeaponUpgradeStateComponent state)
    {
        switch (def.Type)
        {
            case WeaponUpgradeType.SpawnItem:
                if (spawnItems && def.SpawnProtoId.HasValue)
                {
                    var coords = Transform(player).Coordinates;
                    for (var i = 0; i < def.SpawnCountPerLevel; i++)
                    {
                        var item = Spawn(def.SpawnProtoId.Value, coords);
                        _stash.Stash(player, item);
                    }
                }
                break;

            default:
                return false;
        }

        return true;
    }
}
