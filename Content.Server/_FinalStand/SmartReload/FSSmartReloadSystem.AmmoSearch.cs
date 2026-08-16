using Content.Shared.Containers.ItemSlots;
using Content.Shared.Power.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;

namespace Content.Server._FinalStand.SmartReload;

public sealed partial class FSSmartReloadSystem : EntitySystem
{
    // A box stays the source until it runs dry; anything else is consumed by the insert.
    private EntityUid NextChainSource(EntityUid used, EntityUid user, EntityWhitelist? whitelist, EntityUid gun)
    {
        if (TryComp<BallisticAmmoProviderComponent>(used, out var box) && box.Count > 0)
            return used;

        return FindBestAmmo(user, whitelist, gun) ?? EntityUid.Invalid;
    }

    // Boxes hand out one round at a time; loose rounds insert directly.
    private bool TryResolveRound(EntityUid used, out EntityUid round)
    {
        round = used;
        if (!HasComp<BallisticAmmoProviderComponent>(used))
            return true;

        if (TrySpawnRoundFromBox(used) is not { } spawned)
            return false;

        round = spawned;
        return true;
    }

    private EntityUid? FindBestMagazine(EntityUid user, EntityUid gun)
    {
        if (!_slots.TryGetSlot(gun, SharedGunSystem.MagazineSlot, out var magSlot))
            return null;

        var currentMag = magSlot.Item;
        var whitelist  = magSlot.Whitelist;

        EntityUid? best      = null;
        var        bestCount = -1;

        if (!TryComp<ContainerManagerComponent>(user, out var mgr))
            return null;

        foreach (var container in mgr.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                CheckMag(item, currentMag, whitelist, ref best, ref bestCount);

                // One level deep (backpack contents, etc.)
                if (!TryComp<ContainerManagerComponent>(item, out var innerMgr))
                    continue;

                foreach (var inner in innerMgr.Containers.Values)
                {
                    foreach (var innerItem in inner.ContainedEntities)
                        CheckMag(innerItem, currentMag, whitelist, ref best, ref bestCount);
                }
            }
        }

        return best;
    }

    private void CheckMag(EntityUid item, EntityUid? currentMag, EntityWhitelist? whitelist,
        ref EntityUid? best, ref int bestCount)
    {
        if (item == currentMag)
            return;
        // Skip mags that are currently loaded inside another gun.
        var parent = Transform(item).ParentUid;
        if (parent.IsValid() && HasComp<GunComponent>(parent))
            return;
        if (!TryComp<BallisticAmmoProviderComponent>(item, out var bal))
            return;
        if (_whitelist.IsWhitelistFail(whitelist, item))
            return;

        if (bal.Count > bestCount)
        {
            best      = item;
            bestCount = bal.Count;
        }
    }

    // skipInside prevents treating the gun being reloaded as its own ammo source.
    private EntityUid? FindBestAmmo(EntityUid user, EntityWhitelist? whitelist, EntityUid skipInside = default)
    {
        if (!TryComp<ContainerManagerComponent>(user, out var mgr))
            return null;

        EntityUid? fallbackBox = null;

        foreach (var container in mgr.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (item == skipInside)
                    continue;

                if (IsValidAmmo(item, whitelist))
                    return item;

                fallbackBox ??= IsCompatibleAmmoBox(item, whitelist) ? item : null;

                if (!TryComp<ContainerManagerComponent>(item, out var innerMgr))
                    continue;

                foreach (var inner in innerMgr.Containers.Values)
                {
                    foreach (var innerItem in inner.ContainedEntities)
                    {
                        if (IsValidAmmo(innerItem, whitelist))
                            return innerItem;

                        fallbackBox ??= IsCompatibleAmmoBox(innerItem, whitelist) ? innerItem : null;

                        if (innerItem == skipInside || !TryComp<ContainerManagerComponent>(innerItem, out var deepMgr))
                            continue;

                        foreach (var deep in deepMgr.Containers.Values)
                        {
                            foreach (var deepItem in deep.ContainedEntities)
                            {
                                if (IsValidAmmo(deepItem, whitelist))
                                    return deepItem;

                                fallbackBox ??= IsCompatibleAmmoBox(deepItem, whitelist) ? deepItem : null;
                            }
                        }
                    }
                }
            }
        }

        return fallbackBox;
    }

    private bool IsCompatibleAmmoBox(EntityUid item, EntityWhitelist? gunWhitelist)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(item, out var bal))
            return false;
        if (bal.Count == 0)
            return false;
        if (gunWhitelist?.Tags == null)
            return false;

        var boxWhitelist = bal.Whitelist;
        if (boxWhitelist?.Tags == null)
            return false;

        foreach (var tag in boxWhitelist.Tags)
        {
            if (gunWhitelist.Tags.Contains(tag))
                return true;
        }
        return false;
    }

    private EntityUid? TrySpawnRoundFromBox(EntityUid box)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(box, out var bal))
            return null;

        if (_containers.TryGetContainer(box, "ballistic-ammo", out var container)
            && container.ContainedEntities.Count > 0)
        {
            var round = container.ContainedEntities[^1];
            _containers.Remove(round, container);
            return round;
        }

        if (bal.UnspawnedCount > 0 && bal.Proto != null)
        {
            _gunSystem.SetBallisticUnspawned((box, bal), bal.UnspawnedCount - 1);
            return Spawn(bal.Proto.Value, Transform(box).Coordinates);
        }

        return null;
    }

    private bool IsValidAmmo(EntityUid item, EntityWhitelist? whitelist)
    {
        return TryComp<CartridgeAmmoComponent>(item, out var cartridge)
               && !cartridge.Spent
               && !_whitelist.IsWhitelistFail(whitelist, item);
    }

    private EntityUid? FindBestPowerCell(EntityUid user, EntityUid? excludeCell)
    {
        if (!TryComp<ContainerManagerComponent>(user, out var mgr))
            return null;

        foreach (var container in mgr.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (IsSpareCell(item, excludeCell))
                    return item;

                if (!TryComp<ContainerManagerComponent>(item, out var innerMgr))
                    continue;

                foreach (var inner in innerMgr.Containers.Values)
                {
                    foreach (var innerItem in inner.ContainedEntities)
                    {
                        if (IsSpareCell(innerItem, excludeCell))
                            return innerItem;
                    }
                }
            }
        }

        return null;
    }

    private bool IsSpareCell(EntityUid item, EntityUid? excludeCell)
    {
        if (item == excludeCell || !HasComp<BatteryComponent>(item))
            return false;

        var parent = Transform(item).ParentUid;
        return !parent.IsValid() || !HasComp<GunComponent>(parent);
    }

    private bool HasMixedAmmoBallistic(BallisticAmmoProviderComponent comp)
    {
        var protos = new HashSet<string>();

        if (comp.UnspawnedCount > 0 && comp.Proto != null)
            protos.Add(comp.Proto.Value.Id);

        foreach (var ent in comp.Entities)
        {
            var proto = MetaData(ent).EntityPrototype?.ID;
            if (proto != null)
                protos.Add(proto);
        }

        return protos.Count > 1;
    }

    private bool HasMixedRevolverAmmo(RevolverAmmoProviderComponent comp)
    {
        var protos = new HashSet<string>();

        for (var i = 0; i < comp.Capacity; i++)
        {
            string? proto = null;

            if (comp.AmmoSlots[i] != null)
                proto = MetaData(comp.AmmoSlots[i]!.Value).EntityPrototype?.ID;
            else if (comp.Chambers[i] == true && comp.FillPrototype != null)
                proto = comp.FillPrototype;

            if (proto != null)
                protos.Add(proto);
        }

        return protos.Count > 1;
    }

    // Truly empty slots TryRevolverInsert can fill (Chambers == null).
    private static int CountNullChambers(RevolverAmmoProviderComponent comp)
    {
        var count = 0;
        for (var i = 0; i < comp.Capacity; i++)
        {
            if (comp.AmmoSlots[i] == null && comp.Chambers[i] == null)
                count++;
        }
        return count;
    }

    // Fired cases (Chambers == false) — need ejecting before a new round can be seated.
    private static int CountSpentChambers(RevolverAmmoProviderComponent comp)
    {
        var count = 0;
        for (var i = 0; i < comp.Capacity; i++)
        {
            if (comp.AmmoSlots[i] == null && comp.Chambers[i] == false)
                count++;
        }
        return count;
    }

    // All reloadable slots (null + spent) — used to detect a fully loaded cylinder.
    private static int CountEmptyChambers(RevolverAmmoProviderComponent comp)
    {
        return CountNullChambers(comp) + CountSpentChambers(comp);
    }
}
