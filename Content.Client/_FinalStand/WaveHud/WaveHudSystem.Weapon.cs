// Resolves the active hand's item and its ammo for the weapon module on the wave HUD.
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudSystem
{
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private EntityWhitelistSystem _magWhitelist = default!;

    private void UpdateWeapon(WaveHudOverlay overlay)
    {
        overlay.WeaponName = null;
        overlay.WeaponLoaded = null;
        overlay.WeaponCapacity = null;
        overlay.WeaponReserve = 0;

        if (_player.LocalEntity is not { } player
            || !_hands.TryGetActiveItem(player, out var active))
        {
            return;
        }

        var held = active.Value;

        overlay.WeaponName = Name(held);

        if (!HasComp<GunComponent>(held))
            return;

        // A magazine-fed gun carries its count on the magazine in the slot, not on itself.
        var source = held;
        if (_itemSlots.TryGetSlot(held, SharedGunSystem.MagazineSlot, out var magSlot))
        {
            if (magSlot.Item is not { } loaded)
            {
                overlay.WeaponLoaded = 0;
                overlay.WeaponCapacity = 0;
                overlay.WeaponReserve = CountSpareMagazines(player, held);
                return;
            }

            source = loaded;
        }

        if (TryGetAmmo(source, out var loadedCount, out var capacity))
        {
            overlay.WeaponLoaded = loadedCount;
            overlay.WeaponCapacity = capacity;
        }

        overlay.WeaponReserve = CountSpareMagazines(player, held);
    }

    private bool TryGetAmmo(EntityUid uid, out int loaded, out int capacity)
    {
        if (TryComp<BallisticAmmoProviderComponent>(uid, out var ballistic))
        {
            loaded = ballistic.UnspawnedCount + ballistic.Entities.Count;
            capacity = ballistic.Capacity;
            return true;
        }

        if (TryComp<SolutionAmmoProviderComponent>(uid, out var solution))
        {
            loaded = solution.Shots;
            capacity = solution.MaxShots;
            return true;
        }

        loaded = 0;
        capacity = 0;
        return false;
    }

    /// <summary>Spare magazines the player is carrying that this gun would accept.</summary>
    private int CountSpareMagazines(EntityUid player, EntityUid gun)
    {
        if (!_itemSlots.TryGetSlot(gun, SharedGunSystem.MagazineSlot, out var magSlot))
            return 0;

        var count = 0;
        Walk(player, 0);
        return count;

        void Walk(EntityUid root, int depth)
        {
            if (!TryComp<ContainerManagerComponent>(root, out var mgr))
                return;

            foreach (var container in mgr.Containers.Values)
            {
                foreach (var item in container.ContainedEntities)
                {
                    if (item != magSlot.Item && !_magWhitelist.IsWhitelistFail(magSlot.Whitelist, item))
                        count++;

                    if (depth < 1)
                        Walk(item, depth + 1);
                }
            }
        }
    }
}
