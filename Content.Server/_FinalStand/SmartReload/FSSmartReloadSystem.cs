using System.Linq;
using Content.Server.Popups;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Power.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.SmartReload;

public sealed class FSSmartReloadSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGunSystem _gunSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;

    private static readonly TimeSpan MagEjectTime    = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan MagInsertTime   = TimeSpan.FromSeconds(0.55);
    private static readonly TimeSpan ShellInsertTime = TimeSpan.FromSeconds(0.55);
    private static readonly TimeSpan ChamberFillTime = TimeSpan.FromSeconds(0.45);

    private enum GunArchetype { Magazine, TubeFed, Revolver, Battery, None }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSSmartReloadMessage>(OnSmartReload);
        SubscribeNetworkEvent<FSEjectMessage>(OnEject);
        SubscribeLocalEvent<MagazineAmmoProviderComponent, FSMagReloadDoAfterEvent>(OnMagReloadComplete);
        SubscribeLocalEvent<ChamberMagazineAmmoProviderComponent, FSMagReloadDoAfterEvent>(OnMagReloadComplete);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, FSShellInsertDoAfterEvent>(OnShellInsertComplete);
        SubscribeLocalEvent<RevolverAmmoProviderComponent, FSChamberFillDoAfterEvent>(OnChamberFillComplete);
    }

    // ---- Validation ----

    private bool TryGetValidGun(NetEntity netGun, ICommonSession? session,
        out EntityUid gun, out EntityUid user)
    {
        gun  = GetEntity(netGun);
        user = session?.AttachedEntity ?? EntityUid.Invalid;

        return user.IsValid()
               && HasComp<GunComponent>(gun)
               && _hands.IsHolding(user, gun);
    }

    // ---- Archetype detection ----

    private GunArchetype Detect(EntityUid gun)
    {
        if (HasComp<RevolverAmmoProviderComponent>(gun))                                          return GunArchetype.Revolver;
        if (HasComp<MagazineAmmoProviderComponent>(gun) ||
            HasComp<ChamberMagazineAmmoProviderComponent>(gun))                                   return GunArchetype.Magazine;
        if (HasComp<BallisticAmmoProviderComponent>(gun))                                         return GunArchetype.TubeFed;
        if (HasComp<BatteryAmmoProviderComponent>(gun))                                           return GunArchetype.Battery;
        return GunArchetype.None;
    }

    // ---- TAP R ----

    private void OnSmartReload(FSSmartReloadMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetValidGun(msg.Gun, args.SenderSession, out var gun, out var user))
            return;

        switch (Detect(gun))
        {
            case GunArchetype.Magazine:
                ReloadMagazine(gun, user);
                break;
            case GunArchetype.TubeFed:
                ReloadTubeFed(gun, user);
                break;
            case GunArchetype.Revolver:
                ReloadRevolver(gun, user);
                break;
            case GunArchetype.Battery:
                ReloadBattery(gun, user);
                break;
            default:
                _popup.PopupEntity("No reload action available.", gun, user);
                break;
        }
    }

    // ---- HOLD R ----

    private void OnEject(FSEjectMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetValidGun(msg.Gun, args.SenderSession, out var gun, out var user))
            return;

        switch (Detect(gun))
        {
            case GunArchetype.Magazine:
                _slots.TryEject(gun, SharedGunSystem.MagazineSlot, user, out _);
                break;

            case GunArchetype.TubeFed:
                if (TryComp<BallisticAmmoProviderComponent>(gun, out var bal))
                    DumpAllTubeShells(gun, bal);
                break;

            case GunArchetype.Revolver:
                if (TryComp<RevolverAmmoProviderComponent>(gun, out var rev))
                    _gunSystem.EmptyRevolver((gun, rev), user);
                break;

            case GunArchetype.Battery:
                // Only eject if the gun has a swappable cell slot.
                if (_slots.TryGetSlot(gun, "gun_cell", out _))
                    _slots.TryEject(gun, "gun_cell", user, out _);
                break;
        }
    }

    // ---- Magazine reload ----

    private void ReloadMagazine(EntityUid gun, EntityUid user)
    {
        var newMag = FindBestMagazine(user, gun);
        if (newMag == null)
        {
            _popup.PopupEntity("No compatible magazine found.", gun, user);
            return;
        }

        if (TryComp<BallisticAmmoProviderComponent>(newMag.Value, out var newBal) && newBal.Count == 0)
        {
            _popup.PopupEntity("All magazines are empty.", gun, user);
            return;
        }

        var hasMag = _slots.TryGetSlot(gun, SharedGunSystem.MagazineSlot, out var slot)
                     && slot!.Item != null;
        var delay  = hasMag ? MagEjectTime + MagInsertTime : MagInsertTime;

        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay,
            new FSMagReloadDoAfterEvent(), eventTarget: gun)
        {
            NeedHand           = true,
            BreakOnMove        = true,
            BreakOnDamage      = true,
            BreakOnHandChange  = true,
            BlockDuplicate     = true,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnMagReloadComplete(EntityUid gun, MagazineAmmoProviderComponent _comp, FSMagReloadDoAfterEvent args)
        => DoMagReload(gun, args);

    private void OnMagReloadComplete(EntityUid gun, ChamberMagazineAmmoProviderComponent _comp, FSMagReloadDoAfterEvent args)
        => DoMagReload(gun, args);

    private void DoMagReload(EntityUid gun, FSMagReloadDoAfterEvent args)
    {
        if (args.Cancelled || !args.User.IsValid())
            return;

        // Eject old mag and try to store in inventory (backpack → pockets → floor)
        if (_slots.TryEject(gun, SharedGunSystem.MagazineSlot, null, out var oldMag) && oldMag != null)
            TryStoreItemInInventory(args.User, oldMag.Value);

        // Re-evaluate best magazine (inventory may have changed during DoAfter)
        var newMag = FindBestMagazine(args.User, gun);
        if (newMag == null)
            return;

        _slots.TryInsert(gun, SharedGunSystem.MagazineSlot, newMag.Value, args.User);
    }

    private void TryStoreItemInInventory(EntityUid user, EntityUid item)
    {
        // 1. Try backpack
        if (_inventory.TryGetSlotEntity(user, "back", out var backpack)
            && TryComp<StorageComponent>(backpack, out var storagComp)
            && _storage.Insert(backpack.Value, item, out _, storageComp: storagComp, playSound: false))
            return;

        // 2. Try pockets
        if (_inventory.TryEquip(user, user, item, "pocket1", silent: true))
            return;
        if (_inventory.TryEquip(user, user, item, "pocket2", silent: true))
            return;

        // 3. Item stays on floor at user's position (already dropped by TryEject with null user)
    }

    // ---- Tube-fed reload ----

    private void ReloadTubeFed(EntityUid gun, EntityUid user)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(gun, out var comp))
            return;

        if (comp.Capacity >= 500)
        {
            _popup.PopupEntity("Minigun can only be reloaded from ammo resupply.", gun, user);
            return;
        }

        if (comp.Count >= comp.Capacity)
        {
            _popup.PopupEntity("Already full.", gun, user);
            return;
        }

        if (HasMixedAmmoBallistic(comp))
        {
            _popup.PopupEntity("Mixed ammo loaded — reload manually.", gun, user);
            return;
        }

        var shell = FindBestAmmo(user, comp.Whitelist);
        if (shell == null)
        {
            _popup.PopupEntity("No compatible ammo found.", gun, user);
            return;
        }

        StartShellInsert(gun, user, shell.Value);
    }

    private void StartShellInsert(EntityUid gun, EntityUid user, EntityUid shell)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, ShellInsertTime,
            new FSShellInsertDoAfterEvent(), eventTarget: gun, used: shell)
        {
            NeedHand           = true,
            BreakOnMove        = true,
            BreakOnDamage      = true,
            BlockDuplicate     = true,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnShellInsertComplete(EntityUid gun, BallisticAmmoProviderComponent comp, FSShellInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Used == null || !args.User.IsValid())
            return;

        var prevCount = comp.Count;
        _gunSystem.TryBallisticInsert((gun, comp), args.Used.Value, args.User);

        // If the insert didn't increase the count, stop chaining to avoid an infinite loop.
        if (comp.Count == prevCount || comp.Count >= comp.Capacity)
            return;

        var next = FindBestAmmo(args.User, comp.Whitelist);
        if (next != null)
            StartShellInsert(gun, args.User, next.Value);
    }

    // ---- Revolver reload ----

    private void ReloadRevolver(EntityUid gun, EntityUid user)
    {
        if (!TryComp<RevolverAmmoProviderComponent>(gun, out var comp))
            return;

        if (CountEmptyChambers(comp) == 0)
        {
            _popup.PopupEntity("Cylinder is full.", gun, user);
            return;
        }

        if (HasMixedRevolverAmmo(comp))
        {
            _popup.PopupEntity("Mixed ammo in cylinder — reload manually.", gun, user);
            return;
        }

        var round = FindBestAmmo(user, comp.Whitelist);
        if (round == null)
        {
            _popup.PopupEntity("No compatible ammo found.", gun, user);
            return;
        }

        StartChamberFill(gun, user, round.Value);
    }

    private void StartChamberFill(EntityUid gun, EntityUid user, EntityUid round)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, ChamberFillTime,
            new FSChamberFillDoAfterEvent(), eventTarget: gun, used: round)
        {
            NeedHand           = true,
            BreakOnMove        = true,
            BreakOnDamage      = true,
            BlockDuplicate     = true,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    private void OnChamberFillComplete(EntityUid gun, RevolverAmmoProviderComponent comp, FSChamberFillDoAfterEvent args)
    {
        if (args.Cancelled || args.Used == null || !args.User.IsValid())
            return;

        var prevEmpty = CountEmptyChambers(comp);
        _gunSystem.TryRevolverInsert((gun, comp), args.Used.Value, args.User);

        // If the insert didn't fill a chamber, stop chaining.
        if (CountEmptyChambers(comp) == prevEmpty || CountEmptyChambers(comp) == 0)
            return;

        var next = FindBestAmmo(args.User, comp.Whitelist);
        if (next != null)
            StartChamberFill(gun, args.User, next.Value);
    }

    // ---- Battery reload ----

    private void ReloadBattery(EntityUid gun, EntityUid user)
    {
        if (!_slots.TryGetSlot(gun, "gun_cell", out _))
        {
            var msg = HasComp<BatterySelfRechargerComponent>(gun)
                ? "This weapon self-recharges."
                : "Needs cell recharger.";
            _popup.PopupEntity(msg, gun, user);
            return;
        }

        _slots.TryEject(gun, "gun_cell", user, out var oldCell);

        var newCell = FindBestPowerCell(user, oldCell);
        if (newCell == null)
        {
            _popup.PopupEntity("No power cell found.", gun, user);
            return;
        }

        _slots.TryInsert(gun, "gun_cell", newCell.Value, user);
    }

    // ---- Dump helpers ----

    private void DumpAllTubeShells(EntityUid gun, BallisticAmmoProviderComponent comp)
    {
        var coords = Transform(gun).Coordinates;

#pragma warning disable RA0002
        foreach (var entity in comp.Entities.ToList())
            _containers.Remove((entity, null, null), comp.Container, force: true);

        comp.Entities.Clear();

        if (comp.Proto != null)
        {
            for (var i = 0; i < comp.UnspawnedCount; i++)
                Spawn(comp.Proto.Value, coords);
        }

        comp.UnspawnedCount = 0;
#pragma warning restore RA0002
        Dirty(gun, comp);
    }

    // ---- Ammo search ----

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

    private EntityUid? FindBestAmmo(EntityUid user, EntityWhitelist? whitelist)
    {
        if (!TryComp<ContainerManagerComponent>(user, out var mgr))
            return null;

        foreach (var container in mgr.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (IsValidAmmo(item, whitelist))
                    return item;

                if (!TryComp<ContainerManagerComponent>(item, out var innerMgr))
                    continue;

                foreach (var inner in innerMgr.Containers.Values)
                {
                    foreach (var innerItem in inner.ContainedEntities)
                    {
                        if (IsValidAmmo(innerItem, whitelist))
                            return innerItem;

                        // Third level: rounds inside ammo boxes that are inside a backpack
                        if (!TryComp<ContainerManagerComponent>(innerItem, out var deepMgr))
                            continue;

                        foreach (var deep in deepMgr.Containers.Values)
                        {
                            foreach (var deepItem in deep.ContainedEntities)
                            {
                                if (IsValidAmmo(deepItem, whitelist))
                                    return deepItem;
                            }
                        }
                    }
                }
            }
        }

        return null;
    }

    private bool IsValidAmmo(EntityUid item, EntityWhitelist? whitelist)
    {
        return HasComp<CartridgeAmmoComponent>(item)
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
                if (item != excludeCell && HasComp<BatteryComponent>(item))
                    return item;
            }
        }

        return null;
    }

    // ---- Mixed-ammo checks ----

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

    private static int CountEmptyChambers(RevolverAmmoProviderComponent comp)
    {
        var count = 0;
        for (var i = 0; i < comp.Capacity; i++)
        {
            // null = truly empty; false = spent case — both can accept a new round
            if (comp.AmmoSlots[i] == null && comp.Chambers[i] != true)
                count++;
        }
        return count;
    }
}
