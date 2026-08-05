// Shared "put this item somewhere on the player" rule for shop purchases and upgrade payloads.
using Content.Shared.Inventory;
using Content.Shared.Storage.EntitySystems;

namespace Content.Server._FinalStand.Shop;

public sealed class FSItemStashSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    public static readonly string[] SlotPriority = ["belt", "suitstorage", "pocket1", "pocket2"];

    public void Stash(EntityUid player, EntityUid item)
    {
        foreach (var slot in SlotPriority)
        {
            if (_inventory.TryEquip(player, item, slot, silent: true))
                return;
        }

        if (_inventory.TryGetSlotEntity(player, "back", out var backpack))
            _storage.Insert(backpack.Value, item, out _, user: player, playSound: false);
    }
}
