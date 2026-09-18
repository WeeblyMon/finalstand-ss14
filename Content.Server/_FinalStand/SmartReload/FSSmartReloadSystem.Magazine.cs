using Content.Shared._FinalStand.Utility;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared.DoAfter;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._FinalStand.SmartReload;

public sealed partial class FSSmartReloadSystem : EntitySystem
{
    private void ReloadMagazine(EntityUid gun, EntityUid user, bool isChainReload = false,
        EntityUid? chosen = null)
    {
        var newMag = chosen ?? FindBestMagazine(user, gun);
        if (newMag == null)
        {
            if (!isChainReload)
                _popup.PopupEntity("No compatible magazine found.", gun, user);
            return;
        }

        if (TryGetMagazineCount(newMag.Value, out var newCount) && newCount == 0)
        {
            if (!isChainReload)
                _popup.PopupEntity("All magazines are empty.", gun, user);
            return;
        }

        var hasMag = FSItemSlots.TryGetSlot(EntityManager, _slots, gun, SharedGunSystem.MagazineSlot, out var slot)
                     && slot!.Item != null;
        var delay  = (hasMag ? MagEjectTime + MagInsertTime : MagInsertTime) * GetReloadMultiplier(user, gun);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay,
            new FSMagReloadDoAfterEvent
            {
                IsChainReload = isChainReload,
                Chosen = chosen is { } c ? GetNetEntity(c) : null,
            }, eventTarget: gun)
        {
            NeedHand           = true,
            BreakOnMove        = false,
            BreakOnDamage      = false,
            BreakOnHandChange  = true,
            BlockDuplicate     = true,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnMagReloadComplete(EntityUid gun, MagazineAmmoProviderComponent comp, FSMagReloadDoAfterEvent args)
    {
        DoMagReload(gun, args);
    }

    private void OnMagReloadComplete(EntityUid gun, ChamberMagazineAmmoProviderComponent comp, FSMagReloadDoAfterEvent args)
    {
        DoMagReload(gun, args);
    }

    private void DoMagReload(EntityUid gun, FSMagReloadDoAfterEvent args)
    {
        if (args.Cancelled || !args.User.IsValid())
            return;

        if (_slots.TryEject(gun, SharedGunSystem.MagazineSlot, null, out var oldMag) && oldMag != null)
            TryStoreItemInInventory(args.User, oldMag.Value);

        EntityUid? newMag = null;
        if (args.Chosen is { } chosenNet)
        {
            var chosen = GetEntity(chosenNet);
            if (chosen.IsValid() && !TerminatingOrDeleted(chosen))
                newMag = chosen;
        }

        newMag ??= FindBestMagazine(args.User, gun);
        if (newMag == null)
            return;

        _slots.TryInsert(gun, SharedGunSystem.MagazineSlot, newMag.Value, args.User);

        var reloaded = new FSGunReloadedEvent(gun, args.User);
        RaiseLocalEvent(ref reloaded);
    }

    private void TryStoreItemInInventory(EntityUid user, EntityUid item)
    {
        if (_inventory.TryGetSlotEntity(user, "back", out var backpack)
            && TryComp<StorageComponent>(backpack, out var storagComp)
            && _storage.Insert(backpack.Value, item, out _, storageComp: storagComp, playSound: false))
            return;

        if (_inventory.TryEquip(user, user, item, "pocket1", silent: true))
            return;
        if (_inventory.TryEquip(user, user, item, "pocket2", silent: true))
            return;

    }
}
