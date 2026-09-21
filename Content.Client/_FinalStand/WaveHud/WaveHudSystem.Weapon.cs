// Resolves the active hand's item and its ammo for the weapon module on the wave HUD.
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Charges.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage;
using Content.Shared.Tools.Components;
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
    [Dependency] private SharedBatterySystem _battery = default!;

    private void UpdateWeapon(WaveHudOverlay overlay)
    {
        overlay.WeaponName = null;
        overlay.WeaponLoaded = null;
        overlay.WeaponCapacity = null;
        overlay.WeaponReserve = 0;
        overlay.HasAmmoChoice = false;
        overlay.HasThrowChoice = false;
        overlay.ItemDetail = null;

        if (_player.LocalEntity is not { } player)
            return;

        overlay.HasThrowChoice = CountGrenadeTypes(player) > 1;

        if (!_hands.TryGetActiveItem(player, out var active))
            return;

        var held = active.Value;

        overlay.WeaponName = Name(held);

        if (!HasComp<GunComponent>(held))
        {
            overlay.ItemDetail = GetItemDetail(held);
            return;
        }

        var ammoEv = new GetAmmoCountEvent();
        RaiseLocalEvent(held, ref ammoEv);

        if (ammoEv.Capacity > 0)
        {
            overlay.WeaponLoaded = ammoEv.Count;
            overlay.WeaponCapacity = ammoEv.Capacity;
        }

        overlay.WeaponReserve = CountSpareMagazines(player, held);
        overlay.HasAmmoChoice = overlay.WeaponReserve > 0;
        overlay.WeaponTakesMagazines = TryGetMagazineSlot(held, out _);
    }

    private string? GetItemDetail(EntityUid held)
    {
        if (TryComp<MultipleToolComponent>(held, out var multi)
            && multi.Entries.Length > 0
            && multi.CurrentEntry < multi.Entries.Length)
        {
            var behaviour = multi.Entries[multi.CurrentEntry].Behavior.FirstOrDefault();
            if (!string.IsNullOrEmpty(behaviour))
                return $"{behaviour.ToLowerInvariant()} - [Z] to switch";
        }

        if (TryComp<BatteryComponent>(held, out var battery) && battery.MaxCharge > 0f)
            return $"{_battery.GetChargeLevel((held, battery)) * 100f:0}% charge";

        if (_solutions.TryGetDrainableSolution(held, out _, out var drainable))
            return $"{drainable.Volume:0}/{drainable.MaxVolume:0}u";

        if (_solutions.TryGetRefillableSolution(held, out _, out var refillable))
            return $"{refillable.Volume:0}/{refillable.MaxVolume:0}u";

        if (TryComp<LimitedChargesComponent>(held, out var charges))
            return $"{charges.LastCharges}/{charges.MaxCharges} charges";

        if (TryComp<StorageComponent>(held, out var storage))
            return storage.Container.ContainedEntities.Count == 1
                ? "1 item"
                : $"{storage.Container.ContainedEntities.Count} items";

        return null;
    }

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
