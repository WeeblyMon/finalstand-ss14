using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server.Popups;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Examine;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Shop;

public sealed class FSShopWeaponSystem : EntitySystem
{
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly FSPlayerUpgradesSystem _upgrades = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const double SellCooldownSeconds = 2.0;
    private const double SellDedupWindowSeconds = 5.0;

    private readonly Dictionary<NetUserId, TimeSpan> _lastSellTime = new();
    private readonly Dictionary<EntityUid, TimeSpan> _recentSells = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSShopWeaponComponent, ExaminedEvent>(OnExamined);
        Subs.BuiEvents<FSShopWeaponComponent>(FSShopWeaponUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnShopOpened);
            subs.Event<FSShopBuyMessage>(OnBuyMessage);
            subs.Event<FSShopUpgradeMessage>(OnUpgradeMessage);
            subs.Event<FSShopRefreshMessage>(OnRefreshMessage);
            subs.Event<FSShopSellMessage>(OnSellMessage);
        });
    }

    private void OnExamined(EntityUid uid, FSShopWeaponComponent comp, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("shop-weapon-examine-price", ("price", comp.Price)));
    }

    private void OnShopOpened(EntityUid uid, FSShopWeaponComponent comp, BoundUIOpenedEvent args)
    {
        SendCurrentLevels(uid, comp, args.Actor);
    }

    private void OnRefreshMessage(EntityUid uid, FSShopWeaponComponent comp, FSShopRefreshMessage args)
    {
        SendCurrentLevels(uid, comp, args.Actor);
    }

    private void SendCurrentLevels(EntityUid uid, FSShopWeaponComponent comp, EntityUid player)
    {
        if (!_mind.TryGetMind(player, out var mindId, out _))
            return;

        if (comp.WeaponProtoId == null)
        {
            SendWeaponLevels(mindId, new Dictionary<string, int>(), "");
            return;
        }

        var levels = CollectShopLevels(player, comp);
        var title = ComputeWeaponTitle(player, comp.WeaponProtoId.Value);
        SendWeaponLevels(mindId, levels, title);
    }

    private void OnBuyMessage(EntityUid uid, FSShopWeaponComponent comp, FSShopBuyMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid())
            return;

        if (!_mind.TryGetMind(player, out var mindId, out _))
            return;

        if (comp.WeaponProtoId == null)
            return;

        // Grenade packs are one per type — block duplicate purchases before charging.
        var existing = FindAllInventoryWeapons(player, comp.WeaponProtoId.Value);
        if (existing.Any(HasComp<FSGrenadePackComponent>))
        {
            _popup.PopupEntity(Loc.GetString("shop-grenade-already-owned"), uid, player);
            return;
        }

        if (!_wallet.TryDeductCredits(mindId, comp.Price))
        {
            _popup.PopupEntity(Loc.GetString("shop-weapon-insufficient-funds"), uid, player);
            return;
        }

        var weapon = Spawn(comp.WeaponProtoId.Value, Transform(player).Coordinates);

        EnsureComp<FSWeaponUpgradeStateComponent>(weapon);

        TryGiveItemToPlayer(player, weapon);

        if (comp.StarterAmmoProtoId != null)
        {
            var coords = Transform(player).Coordinates.Offset(new System.Numerics.Vector2(0.5f, 0.5f));
            for (var i = 0; i < comp.StarterAmmoCount; i++)
            {
                var ammo = Spawn(comp.StarterAmmoProtoId.Value, coords);
                TryStashItemOnPlayer(player, ammo);
            }
        }

        _popup.PopupEntity(Loc.GetString("shop-weapon-purchased"), uid, player);
        var title = ComputeWeaponTitle(player, comp.WeaponProtoId.Value);
        SendWeaponLevels(mindId, new Dictionary<string, int>(), title);
    }

    private void OnUpgradeMessage(EntityUid uid, FSShopWeaponComponent comp, FSShopUpgradeMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid())
            return;

        if (!_mind.TryGetMind(player, out var mindId, out _))
            return;

        WeaponUpgradeDef? def = null;
        foreach (var upgrade in comp.Upgrades)
        {
            if (upgrade.Id == args.UpgradeId) { def = upgrade; break; }
        }
        if (def == null)
            return;

        if (comp.WeaponProtoId == null)
            return;

        EntProtoId targetProto;
        List<EntProtoId>? aliases;
        if (def.TargetWeaponProtoId != null)
        {
            targetProto = def.TargetWeaponProtoId.Value;
            aliases = null;
        }
        else
        {
            targetProto = comp.WeaponProtoId.Value;
            aliases = comp.WeaponProtoIdAliases.Count > 0 ? comp.WeaponProtoIdAliases : null;
        }

        var weapon = FindHeldWeapon(player, targetProto, aliases);
        if (weapon == null)
        {
            _popup.PopupEntity(Loc.GetString("shop-upgrade-hold-target", ("proto", targetProto.Id)), uid, player);
            return;
        }

        var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon.Value);
        var currentLevel = state.Levels.GetValueOrDefault(def.Id, 0);

        if (currentLevel >= def.MaxLevel)
        {
            _popup.PopupEntity(Loc.GetString("shop-upgrade-max-level"), uid, player);
            return;
        }

        if (def.RequiresUpgrade != null)
        {
            var shopWeapon = FindHeldWeapon(player, comp.WeaponProtoId.Value,
                comp.WeaponProtoIdAliases.Count > 0 ? comp.WeaponProtoIdAliases : null);
            var shopState = shopWeapon != null
                ? CompOrNull<FSWeaponUpgradeStateComponent>(shopWeapon.Value)
                : null;
            if (shopState == null
                || shopState.Levels.GetValueOrDefault(def.RequiresUpgrade, 0) <= 0)
            {
                _popup.PopupEntity(Loc.GetString("shop-upgrade-locked"), uid, player);
                return;
            }
        }

        if (def.Type == WeaponUpgradeType.Akimbo)
        {
            var hasFreeHand = false;
            if (TryComp<HandsComponent>(player, out var playerHands))
            {
                foreach (var handName in playerHands.SortedHands)
                {
                    if (!_hands.TryGetHeldItem((player, playerHands), handName, out _))
                    {
                        hasFreeHand = true;
                        break;
                    }
                }
            }
            if (!hasFreeHand)
            {
                _popup.PopupEntity(Loc.GetString("shop-upgrade-no-free-hand"), uid, player);
                return;
            }
        }

        var cost = GetUpgradeLevelCost(def, currentLevel + 1);
        if (!_wallet.TryDeductCredits(mindId, cost))
        {
            _popup.PopupEntity(Loc.GetString("shop-weapon-insufficient-funds"), uid, player);
            return;
        }

        var newLevel = currentLevel + 1;
        var isFirstUpgradeEver = state.Levels.Count == 0;
        state.Levels[def.Id] = newLevel;
        state.TotalSpent += cost;
        _upgrades.ApplySingleUpgrade(weapon.Value, player, def, newLevel);

        if (isFirstUpgradeEver)
            MarkAsUpgraded(weapon.Value);

        _popup.PopupEntity(Loc.GetString("shop-upgrade-purchased", ("name", def.Name)), uid, player);
        var title = comp.WeaponProtoId != null ? ComputeWeaponTitle(player, comp.WeaponProtoId.Value) : "";
        SendWeaponLevels(mindId, CollectShopLevels(player, comp), title);
    }

    private Dictionary<string, int> CollectShopLevels(EntityUid player, FSShopWeaponComponent comp)
    {
        var merged = new Dictionary<string, int>();
        if (comp.WeaponProtoId == null)
            return merged;

        var protos = new HashSet<EntProtoId> { comp.WeaponProtoId.Value };
        foreach (var alias in comp.WeaponProtoIdAliases)
            protos.Add(alias);
        foreach (var up in comp.Upgrades)
        {
            if (up.TargetWeaponProtoId is { } t)
                protos.Add(t);
        }

        foreach (var proto in protos)
        {
            var weapon = FindHeldWeapon(player, proto);
            if (weapon == null || !TryComp<FSWeaponUpgradeStateComponent>(weapon.Value, out var st))
                continue;
            foreach (var (k, v) in st.Levels)
                merged[k] = v;
        }
        return merged;
    }

    private void OnSellMessage(EntityUid shopUid, FSShopWeaponComponent comp, FSShopSellMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid() || comp.WeaponProtoId == null)
            return;

        if (!_mind.TryGetMind(player, out var mindId, out var mind) || mind.UserId == null)
            return;

        var userId = mind.UserId.Value;

        var now = _timing.CurTime;
        if (_lastSellTime.TryGetValue(userId, out var lastSell)
            && (now - lastSell).TotalSeconds < SellCooldownSeconds)
            return;

        CleanRecentSells(now);
        var candidates = FindAllInventoryWeapons(player, comp.WeaponProtoId.Value);
        if (candidates.Count == 0)
        {
            SendSellResponse(userId, success: false, "No copy of this weapon found in inventory.");
            return;
        }

        candidates.Sort((a, b) =>
        {
            var aSum = TryComp<FSWeaponUpgradeStateComponent>(a, out var as_) ? as_.Levels.Values.Sum() : 0;
            var bSum = TryComp<FSWeaponUpgradeStateComponent>(b, out var bs_) ? bs_.Levels.Values.Sum() : 0;
            return aSum.CompareTo(bSum);
        });
        var weapon = candidates[0];

        if (_recentSells.ContainsKey(weapon))
            return;

        var combinedSpent = TryComp<FSWeaponUpgradeStateComponent>(weapon, out var ws) ? ws.TotalSpent : 0;

        var baseRefund    = (int)(comp.Price * 0.40f);
        var upgradeRefund = (int)(combinedSpent * 0.40f);
        var totalRefund   = baseRefund + upgradeRefund;
        totalRefund = (int)(Math.Round(totalRefund / 50.0) * 50);
        totalRefund = Math.Max(0, totalRefund);
        try
        {
            QueueDel(weapon);
            CleanupAmmoForWeapon(player, comp);
            _wallet.GiveCredits(mindId, totalRefund);

            _lastSellTime[userId] = now;
            _recentSells[weapon] = now;
        }
        catch (Exception ex)
        {
            Log.Error($"[FSSell] Primary deletion failed for weapon {weapon}, player {player}: {ex}");
            SendSellResponse(userId, success: false, "Internal error during sell.");
            return;
        }

        _popup.PopupEntity($"Sold for ${totalRefund:N0}.", shopUid, player);
        SendSellResponse(userId, success: true, "");

        SendWeaponLevels(mindId, new Dictionary<string, int>(), "");
    }

    private void CleanRecentSells(TimeSpan now)
    {
        var stale = new List<EntityUid>();
        foreach (var (uid, time) in _recentSells)
        {
            if ((now - time).TotalSeconds >= SellDedupWindowSeconds)
                stale.Add(uid);
        }
        foreach (var uid in stale)
            _recentSells.Remove(uid);
    }

    private void SendSellResponse(NetUserId userId, bool success, string reason)
    {
        if (!_playerManager.TryGetSessionById(userId, out var session))
            return;
        if (success)
            RaiseNetworkEvent(new FSShopSellCompletedEvent(), Filter.SinglePlayer(session));
        else
            RaiseNetworkEvent(new FSShopSellFailedEvent(reason), Filter.SinglePlayer(session));
    }

    private List<EntityUid> FindAllInventoryWeapons(EntityUid player, EntProtoId protoId)
    {
        var results = new List<EntityUid>();
        var targetId = (string) protoId;

        if (TryComp<HandsComponent>(player, out var hands))
        {
            foreach (var handName in hands.SortedHands)
            {
                if (!_hands.TryGetHeldItem((player, hands), handName, out var held) || held == null)
                    continue;
                if (MetaData(held.Value).EntityPrototype?.ID == targetId
                    && HasComp<FSWeaponUpgradeStateComponent>(held.Value))
                    results.Add(held.Value);
            }
        }

        foreach (var slot in new[] { "belt", "suitstorage", "pocket1", "pocket2" })
        {
            if (!_inventory.TryGetSlotEntity(player, slot, out var slotEnt) || slotEnt == null)
                continue;
            if (MetaData(slotEnt.Value).EntityPrototype?.ID == targetId
                && HasComp<FSWeaponUpgradeStateComponent>(slotEnt.Value))
                results.Add(slotEnt.Value);
        }

        if (_inventory.TryGetSlotEntity(player, "back", out var backpack) && backpack != null)
        {
            if (TryComp<ContainerManagerComponent>(backpack.Value, out var cm))
            {
                foreach (var container in cm.Containers.Values)
                {
                    foreach (var item in container.ContainedEntities)
                    {
                        if (MetaData(item).EntityPrototype?.ID == targetId
                            && HasComp<FSWeaponUpgradeStateComponent>(item))
                            results.Add(item);
                    }
                }
            }
        }

        return results;
    }

    private static readonly string[] InventorySlotPriority = ["belt", "suitstorage", "pocket1", "pocket2"];

    private void TryGiveItemToPlayer(EntityUid player, EntityUid item)
    {
        // Grenade packs are pocket items — they must stay in inventory, never in hand.
        if (HasComp<FSGrenadePackComponent>(item))
        {
            TryStashItemOnPlayer(player, item);
            return;
        }

        if (_hands.TryPickupAnyHand(player, item))
            return;

        TryStashItemOnPlayer(player, item);
    }

    private void TryStashItemOnPlayer(EntityUid player, EntityUid item)
    {
        foreach (var slot in InventorySlotPriority)
        {
            if (_inventory.TryEquip(player, item, slot, silent: true))
                return;
        }

        if (_inventory.TryGetSlotEntity(player, "back", out var backpack))
            _storage.Insert(backpack.Value, item, out _, user: player, playSound: false);
    }

    private void MarkAsUpgraded(EntityUid weapon)
    {
        var meta = MetaData(weapon);
        if (!meta.EntityName.EndsWith(" (Upgraded)"))
            _metaData.SetEntityName(weapon, meta.EntityName + " (Upgraded)", meta);
    }

    private string ComputeWeaponTitle(EntityUid player, EntProtoId protoId)
    {
        if (!TryComp<HandsComponent>(player, out var hands))
            return "";

        var matches = new List<(EntityUid uid, string handName)>();
        foreach (var handName in hands.SortedHands)
        {
            if (!_hands.TryGetHeldItem((player, hands), handName, out var held) || held == null)
                continue;
            if (MetaData(held.Value).EntityPrototype?.ID == (string) protoId)
                matches.Add((held.Value, handName));
        }

        if (matches.Count == 0)
            return "";

        var rawName = MetaData(matches[0].uid).EntityName;
        var baseName = rawName.EndsWith(" (Upgraded)")
            ? rawName[..^" (Upgraded)".Length]
            : rawName;
        baseName = Capitalize(baseName);

        var handLabel = HandLabel(player, hands, matches[0].handName);

        return matches.Count == 1
            ? $"{baseName} ({handLabel})"
            : $"{baseName} No. 1 ({handLabel})";
    }

    private string HandLabel(EntityUid player, HandsComponent hands, string handName)
    {
        if (!_hands.TryGetHand((player, hands), handName, out var hand))
            return "Hand";
        return hand.Value.Location switch
        {
            HandLocation.Left  => "Left Hand",
            HandLocation.Right => "Right Hand",
            _                  => "Hand",
        };
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private EntityUid? FindHeldWeapon(EntityUid player, EntProtoId protoId)
        => FindHeldWeapon(player, protoId, null);

    private EntityUid? FindHeldWeapon(EntityUid player, EntProtoId protoId, List<EntProtoId>? aliases)
    {
        if (TryComp<HandsComponent>(player, out var hands))
        {
            foreach (var handName in hands.SortedHands)
            {
                if (!_hands.TryGetHeldItem((player, hands), handName, out var held))
                    continue;
                var heldProto = MetaData(held.Value).EntityPrototype?.ID;
                if (heldProto == null)
                    continue;
                if (heldProto == (string) protoId)
                    return held;
                if (aliases != null)
                {
                    foreach (var alias in aliases)
                    {
                        if (heldProto == (string) alias)
                            return held;
                    }
                }
            }
        }

        // Also check pocket/belt/suit-storage slots for non-handheld items (e.g. grenade packs).
        foreach (var slot in InventorySlotPriority)
        {
            if (!_inventory.TryGetSlotEntity(player, slot, out var slotEnt) || slotEnt == null)
                continue;
            var slotProto = MetaData(slotEnt.Value).EntityPrototype?.ID;
            if (slotProto == null)
                continue;
            if (slotProto == (string) protoId)
                return slotEnt;
            if (aliases != null)
            {
                foreach (var alias in aliases)
                {
                    if (slotProto == (string) alias)
                        return slotEnt;
                }
            }
        }

        return null;
    }

    private void CleanupAmmoForWeapon(EntityUid player, FSShopWeaponComponent shopComp)
    {
        var ammoProtos = new HashSet<string>();
        if (shopComp.StarterAmmoProtoId != null)
            ammoProtos.Add((string)shopComp.StarterAmmoProtoId.Value);
        foreach (var upgrade in shopComp.Upgrades)
        {
            if (upgrade.Type == WeaponUpgradeType.SpawnItem && upgrade.SpawnProtoId != null)
                ammoProtos.Add((string)upgrade.SpawnProtoId.Value);
        }
        if (ammoProtos.Count == 0)
            return;

        var toDelete = new List<EntityUid>();

        if (TryComp<HandsComponent>(player, out var hands))
        {
            foreach (var handName in hands.SortedHands)
            {
                if (!_hands.TryGetHeldItem((player, hands), handName, out var held) || held == null)
                    continue;
                if (ammoProtos.Contains(MetaData(held.Value).EntityPrototype?.ID ?? ""))
                    toDelete.Add(held.Value);
            }
        }

        foreach (var slot in new[] { "belt", "suitstorage", "pocket1", "pocket2" })
        {
            if (!_inventory.TryGetSlotEntity(player, slot, out var slotEnt) || slotEnt == null)
                continue;
            if (ammoProtos.Contains(MetaData(slotEnt.Value).EntityPrototype?.ID ?? ""))
                toDelete.Add(slotEnt.Value);
        }

        if (_inventory.TryGetSlotEntity(player, "back", out var backpack) && backpack != null)
        {
            if (TryComp<ContainerManagerComponent>(backpack.Value, out var cm))
            {
                foreach (var container in cm.Containers.Values)
                {
                    foreach (var item in container.ContainedEntities)
                    {
                        if (ammoProtos.Contains(MetaData(item).EntityPrototype?.ID ?? ""))
                            toDelete.Add(item);
                    }
                }
            }
        }

        foreach (var item in toDelete)
        {
            if (Exists(item))
                QueueDel(item);
        }
    }

    private static readonly float[] LevelCostMults = [1.0f, 1.3f, 1.8f, 2.6f, 3.6f];

    private static int GetUpgradeLevelCost(WeaponUpgradeDef def, int level)
    {
        var mult = level > 0 && level <= LevelCostMults.Length
            ? LevelCostMults[level - 1]
            : 3.6f + (level - LevelCostMults.Length) * 1.2f;
        return (int)MathF.Round(def.BaseCost * mult);
    }

    private void SendWeaponLevels(EntityUid mindId, Dictionary<string, int> levels, string title = "")
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            return;
        RaiseNetworkEvent(new UpgradeLevelsUpdatedEvent(new Dictionary<string, int>(levels), title),
            Filter.SinglePlayer(session));
    }
}
