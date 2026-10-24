using Content.Shared._FinalStand.Utility;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared.DoAfter;
using Content.Shared.Storage;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;

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
                _popup.PopupEntity(Loc.GetString("fs-smart-reload-no-magazine"), gun, user);
            return;
        }

        if (TryGetMagazineCount(newMag.Value, out var newCount) && newCount == 0)
        {
            if (!isChainReload)
                _popup.PopupEntity(Loc.GetString("fs-smart-reload-magazines-empty"), gun, user);
            return;
        }

        var hasMag = FSItemSlots.TryGetSlot(EntityManager, _slots, gun, SharedGunSystem.MagazineSlot, out var slot)
                     && slot!.Item != null;
        var multiplier = GetReloadMultiplier(user, gun);
        var profile = CompOrNull<FSReloadProfileComponent>(gun);
        var delay  = (hasMag ? MagEjectTime + MagInsertTime : MagInsertTime) * (profile?.TimeMultiplier ?? 1f) * multiplier;

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

        if (!_doAfter.TryStartDoAfter(doAfterArgs) || profile is not { Sound: { } sound })
            return;

        var pitch = Math.Clamp(1f / multiplier, 1f, 2f);
        profile.Playing = _audio.PlayPvs(sound, gun, AudioParams.Default.WithPitchScale(pitch))?.Entity;
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
        if (TryComp<FSReloadProfileComponent>(gun, out var profile))
        {
            if (args.Cancelled)
                _audio.Stop(profile.Playing);

            profile.Playing = null;
        }

        if (args.Cancelled || !args.User.IsValid())
            return;

        if (_slots.TryEject(gun, SharedGunSystem.MagazineSlot, null, out var oldMag) && oldMag != null)
            TryStoreItemInInventory(args.User, oldMag.Value);

        EntityUid? newMag = null;
        if (args.Chosen is { } chosenNet)
        {
            var chosen = GetEntity(chosenNet);
            if (chosen.IsValid() && !TerminatingOrDeleted(chosen) && _transform.ContainsEntity(args.User, chosen))
                newMag = chosen;
        }

        newMag ??= FindBestMagazine(args.User, gun);
        if (newMag == null)
            return;

        _slots.TryInsert(gun, SharedGunSystem.MagazineSlot, newMag.Value, args.User);

        var reloaded = new FSGunReloadedEvent(gun, args.User);
        RaiseLocalEvent(ref reloaded);
    }

    private static readonly string[] StorageSlots = ["back", "belt", "suitstorage"];
    private static readonly string[] EquipSlots = ["pocket1", "pocket2"];

    private void TryStoreItemInInventory(EntityUid user, EntityUid item)
    {
        foreach (var slot in StorageSlots)
        {
            if (_inventory.TryGetSlotEntity(user, slot, out var container)
                && TryComp<StorageComponent>(container, out var storage)
                && _storage.Insert(container.Value, item, out _, storageComp: storage, playSound: false))
                return;
        }

        foreach (var slot in EquipSlots)
        {
            if (_inventory.TryEquip(user, user, item, slot, silent: true))
                return;
        }

        _hands.TryPickupAnyHand(user, item);
    }
}
