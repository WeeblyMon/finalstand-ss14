using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server.Popups;
using Content.Shared._FinalStand.Akimbo;
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

    // TODO(finalstand): tune sell cooldown duration
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

        var weapon = FindHeldWeapon(player, comp.WeaponProtoId.Value);
        var levels = (weapon != null && TryComp<FSWeaponUpgradeStateComponent>(weapon.Value, out var state))
            ? state.Levels
            : new Dictionary<string, int>();

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

        if (!_wallet.TryDeductCredits(mindId, comp.Price))
        {
            _popup.PopupEntity(Loc.GetString("shop-weapon-insufficient-funds"), uid, player);
            return;
        }

        var weapon = Spawn(comp.WeaponProtoId.Value, Transform(player).Coordinates);

        // Mark as FS shop weapon immediately so it is sellable even before any upgrade.
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

        var weapon = FindHeldWeapon(player, comp.WeaponProtoId.Value);
        if (weapon == null)
        {
            _popup.PopupEntity("Hold the weapon to upgrade it.", uid, player);
            return;
        }

        var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon.Value);
        var currentLevel = state.Levels.GetValueOrDefault(def.Id, 0);

        if (currentLevel >= def.MaxLevel)
        {
            _popup.PopupEntity(Loc.GetString("shop-upgrade-max-level"), uid, player);
            return;
        }

        // Akimbo pre-flight: confirm a free hand exists before charging credits.
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
                _popup.PopupEntity("No free hand for akimbo.", uid, player);
                return;
            }
        }

        var cost = def.BaseCost * (currentLevel + 1);
        if (!_wallet.TryDeductCredits(mindId, cost))
        {
            _popup.PopupEntity(Loc.GetString("shop-weapon-insufficient-funds"), uid, player);
            return;
        }

        var newLevel = currentLevel + 1;
        var isFirstUpgradeEver = state.Levels.Count == 0;
        state.Levels[def.Id] = newLevel;
        state.TotalSpent += cost;  // FINALSTAND: track cumulative spend for sell refund
        _upgrades.ApplySingleUpgrade(weapon.Value, player, def, newLevel);

        if (isFirstUpgradeEver)
            MarkAsUpgraded(weapon.Value);

        // Mirror the upgrade to the akimbo partner.
        if (TryComp<FSAkimboGunComponent>(weapon.Value, out var akimbo)
            && akimbo.PairedGun != null
            && akimbo.PairedGun.Value.IsValid())
        {
            var paired = akimbo.PairedGun.Value;
            var pairedState = EnsureComp<FSWeaponUpgradeStateComponent>(paired);

            if (def.Type == WeaponUpgradeType.Akimbo)
            {
                // Fresh akimbo spawn — re-apply all previously purchased upgrades to partner
                // and set partner TotalSpent to match the mirrored upgrade costs.
                var partnerSpent = 0;
                foreach (var prevDef in comp.Upgrades)
                {
                    if (prevDef.Id == def.Id) continue; // skip Akimbo itself
                    if (!state.Levels.TryGetValue(prevDef.Id, out var prevLevel) || prevLevel == 0)
                        continue;
                    pairedState.Levels[prevDef.Id] = prevLevel;
                    for (var lvl = 1; lvl <= prevLevel; lvl++)
                    {
                        _upgrades.ApplySingleUpgrade(paired, player, prevDef, lvl, spawnItems: false);
                        partnerSpent += prevDef.BaseCost * lvl;
                    }
                }
                pairedState.TotalSpent = partnerSpent;
            }
            else
            {
                // Normal case: mirror just this upgrade level delta.
                pairedState.Levels[def.Id] = newLevel;
                pairedState.TotalSpent += cost;  // FINALSTAND: mirror spend to partner
                _upgrades.ApplySingleUpgrade(paired, player, def, newLevel, spawnItems: false);
            }

            MarkAsUpgraded(paired);
        }

        _popup.PopupEntity(Loc.GetString("shop-upgrade-purchased", ("name", def.Name)), uid, player);
        var title = comp.WeaponProtoId != null ? ComputeWeaponTitle(player, comp.WeaponProtoId.Value) : "";
        SendWeaponLevels(mindId, state.Levels, title);
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

        EntityUid? partner = null;
        if (TryComp<FSAkimboGunComponent>(weapon, out var akimboComp)
            && akimboComp.PairedGun.HasValue
            && akimboComp.PairedGun.Value.IsValid()
            && Exists(akimboComp.PairedGun.Value))
        {
            partner = akimboComp.PairedGun.Value;
        }

        var primarySpent  = TryComp<FSWeaponUpgradeStateComponent>(weapon, out var ws) ? ws.TotalSpent : 0;
        var partnerSpent  = partner.HasValue
            && TryComp<FSWeaponUpgradeStateComponent>(partner.Value, out var ps) ? ps.TotalSpent : 0;
        var combinedSpent = primarySpent + partnerSpent;

        var baseRefund    = (int)(comp.Price * 0.40f);
        var upgradeRefund = (int)(combinedSpent * 0.40f);
        var totalRefund   = baseRefund + upgradeRefund;
        totalRefund = (int)(Math.Round(totalRefund / 50.0) * 50);
        totalRefund = Math.Max(0, totalRefund);
        // TODO(finalstand): verify money system handles adding positive refund to a 0-credit player
        try
        {
            QueueDel(weapon);

            if (partner.HasValue)
            {
                try
                {
                    if (Exists(partner.Value))
                        QueueDel(partner.Value);
                }
                catch (Exception ex)
                {
                    Log.Error($"[FSSell] Akimbo partner deletion failed for {partner.Value}, player {player}: {ex}");
                }
            }

            _wallet.GiveCredits(mindId, totalRefund);

            _lastSellTime[userId] = now;
            _recentSells[weapon] = now;
            if (partner.HasValue)
                _recentSells[partner.Value] = now;
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

        // TODO(finalstand): decide whether nested container search (bag-in-bag) is needed
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

    // ---- Helpers ----

    private static readonly string[] InventorySlotPriority = ["belt", "suitstorage", "pocket1", "pocket2"];

    private void TryGiveItemToPlayer(EntityUid player, EntityUid item)
    {
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
    {
        if (!TryComp<HandsComponent>(player, out var hands))
            return null;

        foreach (var handName in hands.SortedHands)
        {
            if (!_hands.TryGetHeldItem((player, hands), handName, out var held))
                continue;
            if (MetaData(held.Value).EntityPrototype?.ID == (string) protoId)
                return held;
        }

        return null;
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
