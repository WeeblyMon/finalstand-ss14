using System.Linq;
using Content.Server._FinalStand.Perks;
using Content.Server.Popups;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Power.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

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
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly PerkSystem _perks = default!;

    private static readonly ProtoId<TagPrototype> HandGrenadeTag = "HandGrenade";

    // Tracks active shell-insert do-afters per gun (for cleanup bookkeeping).
    private readonly Dictionary<EntityUid, DoAfterId> _activeShellInserts = new();

    // Guns that fired a shot while a shell-insert chain was running.
    // The chain aborts at the next OnShellInsertComplete rather than via Cancel()
    // (Cancel leaves a stale BlockDuplicate entry that breaks subsequent R presses).
    private readonly HashSet<EntityUid> _reloadAborted = new();

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
        SubscribeNetworkEvent<FSQuickGrenadeMessage>(OnQuickGrenade);
        SubscribeLocalEvent<MagazineAmmoProviderComponent, FSMagReloadDoAfterEvent>(OnMagReloadComplete);
        SubscribeLocalEvent<ChamberMagazineAmmoProviderComponent, FSMagReloadDoAfterEvent>(OnMagReloadComplete);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, FSShellInsertDoAfterEvent>(OnShellInsertComplete);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, AmmoShotEvent>(OnBallisticGunFired);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ComponentRemove>(OnBallisticRemoved);
        SubscribeLocalEvent<RevolverAmmoProviderComponent, FSChamberFillDoAfterEvent>(OnChamberFillComplete);
    }

    private bool TryGetValidGun(NetEntity netGun, ICommonSession? session,
        out EntityUid gun, out EntityUid user)
    {
        gun  = GetEntity(netGun);
        user = session?.AttachedEntity ?? EntityUid.Invalid;

        return user.IsValid()
               && HasComp<GunComponent>(gun)
               && _hands.IsHolding(user, gun);
    }
    private GunArchetype Detect(EntityUid gun)
    {
        if (HasComp<RevolverAmmoProviderComponent>(gun))                                          return GunArchetype.Revolver;
        if (HasComp<MagazineAmmoProviderComponent>(gun) ||
            HasComp<ChamberMagazineAmmoProviderComponent>(gun))                                   return GunArchetype.Magazine;
        if (HasComp<BallisticAmmoProviderComponent>(gun))                                         return GunArchetype.TubeFed;
        if (HasComp<BatteryAmmoProviderComponent>(gun))                                           return GunArchetype.Battery;
        return GunArchetype.None;
    }

    // R Tap

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

    // Hold R to eject (magazine, tube shells, revolver rounds, or battery cell depending on gun type)

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

    // Reload Magazine-fed guns (rifles, SMGs, etc.)

    private void ReloadMagazine(EntityUid gun, EntityUid user, bool isChainReload = false)
    {
        var newMag = FindBestMagazine(user, gun);
        if (newMag == null)
        {
            if (!isChainReload)
                _popup.PopupEntity("No compatible magazine found.", gun, user);
            return;
        }

        if (TryComp<BallisticAmmoProviderComponent>(newMag.Value, out var newBal) && newBal.Count == 0)
        {
            if (!isChainReload)
                _popup.PopupEntity("All magazines are empty.", gun, user);
            return;
        }

        var hasMag = _slots.TryGetSlot(gun, SharedGunSystem.MagazineSlot, out var slot)
                     && slot!.Item != null;
        var delay  = (hasMag ? MagEjectTime + MagInsertTime : MagInsertTime) * GetReloadMultiplier(user, gun);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, delay,
            new FSMagReloadDoAfterEvent { IsChainReload = isChainReload }, eventTarget: gun)
        {
            NeedHand           = true,
            BreakOnMove        = false,
            BreakOnDamage      = true,
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

    private void ReloadTubeFed(EntityUid gun, EntityUid user, bool isChainReload = false)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(gun, out var comp))
            return;

        if (comp.Capacity >= 500)
        {
            if (!isChainReload)
                _popup.PopupEntity("Minigun can only be reloaded from ammo resupply.", gun, user);
            return;
        }

        if (comp.Count >= comp.Capacity)
        {
            if (!isChainReload)
                _popup.PopupEntity("Already full.", gun, user);
            return;
        }

        if (HasMixedAmmoBallistic(comp))
        {
            if (!isChainReload)
                _popup.PopupEntity("Mixed ammo loaded — reload manually.", gun, user);
            return;
        }

        var shell = FindBestAmmo(user, comp.Whitelist);
        if (shell == null)
        {
            if (!isChainReload)
                _popup.PopupEntity("No compatible ammo found.", gun, user);
            return;
        }

        if (_activeShellInserts.ContainsKey(gun))
            return;

        _reloadAborted.Remove(gun);
        StartShellInsert(gun, user, shell.Value, isChainReload);
    }

    private void StartShellInsert(EntityUid gun, EntityUid user, EntityUid shell, bool isChainReload = false)
    {
        var insertTime = TryComp<FSWeaponUpgradeStateComponent>(gun, out var upg) && upg.SpeedLoaderEnabled
            ? TimeSpan.FromSeconds(0.05)
            : ShellInsertTime * GetReloadMultiplier(user, gun);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, insertTime,
            new FSShellInsertDoAfterEvent { IsChainReload = isChainReload }, eventTarget: gun, used: shell)
        {
            NeedHand      = true,
            BreakOnMove   = false,
            BreakOnDamage = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs, out var id))
            _activeShellInserts[gun] = id.Value;
        else
            _activeShellInserts.Remove(gun);
    }

    private void OnBallisticGunFired(EntityUid gun, BallisticAmmoProviderComponent _, AmmoShotEvent args)
    {
        if (_activeShellInserts.ContainsKey(gun))
            _reloadAborted.Add(gun);
    }

    private void OnBallisticRemoved(EntityUid gun, BallisticAmmoProviderComponent _, ComponentRemove args)
    {
        _activeShellInserts.Remove(gun);
        _reloadAborted.Remove(gun);
    }

    private void OnShellInsertComplete(EntityUid gun, BallisticAmmoProviderComponent comp, FSShellInsertDoAfterEvent args)
    {
        if (args.Cancelled || args.Used == null || !args.User.IsValid())
        {
            _activeShellInserts.Remove(gun);
            return;
        }

        if (_reloadAborted.Remove(gun))
        {
            _activeShellInserts.Remove(gun);
            return;
        }

        var toInsert = args.Used.Value;
        if (HasComp<BallisticAmmoProviderComponent>(toInsert))
        {
            var spawned = TrySpawnRoundFromBox(toInsert);
            if (spawned == null)
                return;
            toInsert = spawned.Value;
        }

        var prevCount = comp.Count;
        _gunSystem.TryBallisticInsert((gun, comp), toInsert, args.User);

        if (comp.Count == prevCount)
        {
            _activeShellInserts.Remove(gun);
            return; // insert failed — stop chain
        }

        if (comp.Count >= comp.Capacity)
        {
            _activeShellInserts.Remove(gun);
            return;
        }

        // Continue filling — carry the chain flag through the loop so it doesn't re-chain mid-reload.
        var nextSource = args.Used.Value;
        if (HasComp<BallisticAmmoProviderComponent>(nextSource)
            && TryComp<BallisticAmmoProviderComponent>(nextSource, out var boxComp)
            && boxComp.Count == 0)
        {
            nextSource = FindBestAmmo(args.User, comp.Whitelist) ?? EntityUid.Invalid;
        }
        else if (!HasComp<BallisticAmmoProviderComponent>(nextSource))
        {
            nextSource = FindBestAmmo(args.User, comp.Whitelist) ?? EntityUid.Invalid;
        }

        if (nextSource.IsValid())
            StartShellInsert(gun, args.User, nextSource, args.IsChainReload);
        else
            _activeShellInserts.Remove(gun);
    }

    // revolver reload

    private void ReloadRevolver(EntityUid gun, EntityUid user, bool isChainReload = false)
    {
        if (!TryComp<RevolverAmmoProviderComponent>(gun, out var comp))
            return;

        var empty = CountEmptyChambers(comp);     // null chambers TryRevolverInsert can fill
        var spent = CountSpentChambers(comp);     // false chambers (fired cases)

        if (empty == 0 && spent == 0)
        {
            if (!isChainReload)
                _popup.PopupEntity("Cylinder is full.", gun, user);
            return;
        }

        if (HasMixedRevolverAmmo(comp))
        {
            if (!isChainReload)
                _popup.PopupEntity("Mixed ammo in cylinder — reload manually.", gun, user);
            return;
        }

        var source = FindBestAmmo(user, comp.Whitelist);
        if (source == null)
        {
            if (!isChainReload)
                _popup.PopupEntity("No compatible ammo found.", gun, user);
            return;
        }

        // TryRevolverInsert only accepts null chambers — eject spent cases first.
        if (empty == 0 && spent > 0)
            _gunSystem.EmptyRevolver((gun, comp), user);

        StartChamberFill(gun, user, source.Value, isChainReload);
    }

    private void StartChamberFill(EntityUid gun, EntityUid user, EntityUid round, bool isChainReload = false)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, ChamberFillTime * GetReloadMultiplier(user, gun),
            new FSChamberFillDoAfterEvent { IsChainReload = isChainReload }, eventTarget: gun, used: round)
        {
            NeedHand           = true,
            BreakOnMove        = false,
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

        // If source is an ammo box, spawn one round from it to insert.
        var toInsert = args.Used.Value;
        if (HasComp<BallisticAmmoProviderComponent>(toInsert))
        {
            var spawned = TrySpawnRoundFromBox(toInsert);
            if (spawned == null)
                return;
            toInsert = spawned.Value;
        }

        var prevNull = CountNullChambers(comp);
        _gunSystem.TryRevolverInsert((gun, comp), toInsert, args.User);

        if (CountNullChambers(comp) == prevNull)
            return; // insert failed — stop loop

        if (CountNullChambers(comp) == 0)
        {
            return;
        }

        // Keep using same box if it was the source; otherwise search again.
        var nextSource = args.Used.Value;
        if (HasComp<BallisticAmmoProviderComponent>(nextSource)
            && TryComp<BallisticAmmoProviderComponent>(nextSource, out var boxComp)
            && boxComp.Count == 0)
        {
            nextSource = FindBestAmmo(args.User, comp.Whitelist) ?? EntityUid.Invalid;
        }
        else if (!HasComp<BallisticAmmoProviderComponent>(nextSource))
        {
            nextSource = FindBestAmmo(args.User, comp.Whitelist) ?? EntityUid.Invalid;
        }

        if (nextSource.IsValid())
            StartChamberFill(gun, args.User, nextSource, args.IsChainReload);
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
        // Skip mags that are currently loaded inside another gun (e.g. akimbo partner's magazine).
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

    private EntityUid? FindBestAmmo(EntityUid user, EntityWhitelist? whitelist)
    {
        if (!TryComp<ContainerManagerComponent>(user, out var mgr))
            return null;

        EntityUid? fallbackBox = null;

        foreach (var container in mgr.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
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

                        if (!TryComp<ContainerManagerComponent>(innerItem, out var deepMgr))
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

        // Take a physically spawned entity from the box's container.
        if (_containers.TryGetContainer(box, "ballistic-ammo", out var container)
            && container.ContainedEntities.Count > 0)
        {
            var round = container.ContainedEntities[^1];
            _containers.Remove(round, container);
            return round;
        }

        // Spawn from unspawned count.
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

    // ---- Quick grenade ----

    private void OnQuickGrenade(FSQuickGrenadeMessage msg, EntitySessionEventArgs args)
    {
        var user = args.SenderSession.AttachedEntity;
        if (user == null)
            return;

        var grenade = FindGrenadeInInventory(user.Value);
        if (grenade == null)
        {
            _popup.PopupEntity("No grenade found.", user.Value, user.Value);
            return;
        }

        // Yank directly out of whatever container it's in (pocket, bag, belt) — no hand needed.
        if (_containers.TryGetContainingContainer(grenade.Value, out var container))
            _containers.Remove(grenade.Value, container, destination: Transform(user.Value).Coordinates);

        // Arm (starts timer fuse on timer grenades; no-op for impact grenades like EMP).
        RaiseLocalEvent(grenade.Value, new UseInHandEvent(user.Value));

        // Throw toward cursor. Fall back to facing direction if cursor is on top of player.
        var playerWorldPos = _transform.GetWorldPosition(user.Value);
        var dir = msg.CursorWorldPos - playerWorldPos;
        if (dir.LengthSquared() < 0.01f)
            dir = _transform.GetWorldRotation(user.Value).ToWorldVec();

        _throwing.TryThrow(grenade.Value, dir, 10f, user.Value);
    }

    private EntityUid? FindGrenadeInInventory(EntityUid user)
    {
        // Check active hand first
        var active = _hands.GetActiveItem(user);
        if (active != null && _tags.HasTag(active.Value, HandGrenadeTag))
            return active.Value;

        if (!TryComp<ContainerManagerComponent>(user, out var mgr))
            return null;

        foreach (var container in mgr.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (_tags.HasTag(item, HandGrenadeTag))
                    return item;

                // One level deep (belt, pockets, backpack)
                if (!TryComp<ContainerManagerComponent>(item, out var innerMgr))
                    continue;

                foreach (var inner in innerMgr.Containers.Values)
                {
                    foreach (var innerItem in inner.ContainedEntities)
                    {
                        if (_tags.HasTag(innerItem, HandGrenadeTag))
                            return innerItem;
                    }
                }
            }
        }

        return null;
    }

    // ---- Reload speed ----

    private float GetReloadMultiplier(EntityUid user, EntityUid gun)
    {
        var mult = _perks.GetReloadMultiplier(user);
        if (TryComp<FSWeaponUpgradeStateComponent>(gun, out var state))
            mult *= state.ReloadSpeedMultiplier;
        return mult;
    }

    // ---- Akimbo sequential reload ----

}
