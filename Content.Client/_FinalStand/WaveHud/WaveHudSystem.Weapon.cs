// Resolves the active hand's item and its ammo for the weapon module on the wave HUD.
using System.Diagnostics.CodeAnalysis;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
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
        overlay.HasAmmoChoice = false;
        overlay.HasThrowChoice = false;

        if (_player.LocalEntity is not { } player)
            return;

        overlay.HasThrowChoice = CountGrenadeTypes(player) > 1;

        if (!_hands.TryGetActiveItem(player, out var active))
            return;

        var held = active.Value;

        overlay.WeaponName = Name(held);

        if (!HasComp<GunComponent>(held))
            return;

        // Every ammo provider answers this - ballistic, battery, revolver, solution - and the
        // magazine provider re-raises it on the loaded magazine. So one event covers laser cells
        // and revolvers as well as mag-fed guns, with no per-type branching to keep in step.
        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(held, ref ammoEv);

        if (ammoEv.Capacity > 0)
        {
            overlay.WeaponLoaded = ammoEv.Count;
            overlay.WeaponCapacity = ammoEv.Capacity;
        }

        // Reserve only means anything for guns that take a magazine; a laser has no spares to count.
        overlay.WeaponReserve = CountSpareMagazines(player, held);
        overlay.HasAmmoChoice = overlay.WeaponReserve > 0;
        overlay.WeaponTakesMagazines = TryGetMagazineSlot(held, out _);
    }

    /// <summary>Distinct grenade pack types carried - one wedge each on the throw wheel.</summary>
    private int CountGrenadeTypes(EntityUid player)
    {
        var seen = new HashSet<GrenadeType>();
        Walk(player, 0);
        return seen.Count;

        void Walk(EntityUid root, int depth)
        {
            if (!TryComp<ContainerManagerComponent>(root, out var mgr))
                return;

            foreach (var container in mgr.Containers.Values)
            {
                foreach (var item in container.ContainedEntities)
                {
                    if (TryComp<FSGrenadePackComponent>(item, out var pack) && pack.Stock > 0)
                        seen.Add(pack.PackType);

                    if (depth < 1)
                        Walk(item, depth + 1);
                }
            }
        }
    }


    private bool TryGetMagazineSlot(EntityUid gun, [NotNullWhen(true)] out ItemSlot? slot) =>
        FSItemSlots.TryGetSlot(EntityManager, _itemSlots, gun, SharedGunSystem.MagazineSlot, out slot);

    /// <summary>Spare magazines the player is carrying that this gun would accept.</summary>
    private int CountSpareMagazines(EntityUid player, EntityUid gun)
    {
        if (!TryGetMagazineSlot(gun, out var magSlot))
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
