using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Research;
using Content.Server._FinalStand.Science;
using Content.Server.Popups;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.UserInterface;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSShopWeaponSystem : EntitySystem
{
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly FSPlayerUpgradesSystem _upgrades = default!;
    [Dependency] private readonly FSItemStashSystem _stash = default!;
    [Dependency] private readonly FSInventorySearchSystem _search = default!;
    [Dependency] private readonly FSResearchSystem _fsResearch = default!;
    [Dependency] private readonly FSResearchStaticGrantSystem _researchStaticGrant = default!;
    [Dependency] private readonly FSScienceOnlySystem _science = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    private const double SellCooldownSeconds = 2.0;
    private const double SellDedupWindowSeconds = 5.0;
    private const string UpgradedSuffix = " (Upgraded)";

    private readonly Dictionary<NetUserId, TimeSpan> _lastSellTime = new();
    private readonly Dictionary<NetEntity, TimeSpan> _recentSells = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<FSShopWeaponComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<FSShopWeaponComponent, ActivatableUIOpenAttemptEvent>(OnOpenAttempt);
        Subs.BuiEvents<FSShopWeaponComponent>(FSShopWeaponUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnShopOpened);
            subs.Event<FSShopBuyMessage>(OnBuyMessage);
            subs.Event<FSShopUpgradeMessage>(OnUpgradeMessage);
            subs.Event<FSShopRefreshMessage>(OnRefreshMessage);
            subs.Event<FSShopSellMessage>(OnSellMessage);
        });
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _lastSellTime.Clear();
        _recentSells.Clear();
    }

    private void OnExamined(EntityUid uid, FSShopWeaponComponent comp, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("shop-weapon-examine-price", ("price", comp.Price)));
    }

    // Research and department locks. Every entry point checks this, not just the UI open.
    private bool TryAccess(EntityUid uid, FSShopWeaponComponent comp, EntityUid player, bool silent = false)
    {
        if (comp.RequiresResearch is { } required && !_fsResearch.IsNodeUnlocked(required))
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString("shop-weapon-locked-research"), uid, player);
            return false;
        }

        if (comp.RequiresScience && !_science.IsScience(player))
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString("shop-weapon-locked-department"), uid, player);
            return false;
        }

        return true;
    }

    private void OnOpenAttempt(EntityUid uid, FSShopWeaponComponent comp, ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!TryAccess(uid, comp, args.User, args.Silent))
            args.Cancel();
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
        var title = ComputeWeaponTitle(player, comp);
        var (acc, nextAcc) = ComputeAccuracy(player, comp);
        SendWeaponLevels(mindId, levels, title, acc, nextAcc);
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

        if (!TryAccess(uid, comp, player))
            return;

        // Grenade packs are one per type — block duplicate purchases before charging.
        var owned = new List<CarriedItem>();
        _search.Collect(player, ShopProtoIds(comp), owned);
        if (owned.Any(c => HasComp<FSGrenadePackComponent>(c.Uid)))
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
                _stash.Stash(player, ammo);
            }
        }

        _popup.PopupEntity(Loc.GetString("shop-weapon-purchased"), uid, player);
        var (buyAcc, buyNextAcc) = ComputeAccuracy(player, comp);
        SendWeaponLevels(mindId, new Dictionary<string, int>(), ComputeWeaponTitle(player, comp), buyAcc, buyNextAcc);
    }

    private void OnUpgradeMessage(EntityUid uid, FSShopWeaponComponent comp, FSShopUpgradeMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid())
            return;

        if (!_mind.TryGetMind(player, out var mindId, out _))
            return;

        if (comp.WeaponProtoId == null)
            return;

        if (!TryAccess(uid, comp, player))
            return;

        WeaponUpgradeDef? def = null;
        foreach (var upgrade in comp.Upgrades)
        {
            if (upgrade.Id == args.UpgradeId) { def = upgrade; break; }
        }
        if (def == null)
            return;

        // An upgrade either retargets a specific weapon, or applies to the shop's own weapon and its aliases.
        var targetIds = def.TargetWeaponProtoId is { } retarget
            ? new HashSet<string> { retarget.Id }
            : ShopProtoIds(comp);

        var found = _search.FindFirst(player, targetIds);
        if (found == null)
        {
            var label = def.TargetWeaponProtoId?.Id ?? comp.WeaponProtoId.Value.Id;
            _popup.PopupEntity(Loc.GetString("shop-upgrade-hold-target", ("proto", label)), uid, player);
            return;
        }

        var weapon = found.Value.Uid;
        var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
        var currentLevel = state.Levels.GetValueOrDefault(def.Id, 0);

        if (currentLevel >= def.MaxLevel)
        {
            _popup.PopupEntity(Loc.GetString("shop-upgrade-max-level"), uid, player);
            return;
        }

        if (def.RequiresUpgrade != null)
        {
            var shopWeapon = _search.FindFirst(player, ShopProtoIds(comp));
            var shopState = shopWeapon != null
                ? CompOrNull<FSWeaponUpgradeStateComponent>(shopWeapon.Value.Uid)
                : null;
            if (shopState == null
                || shopState.Levels.GetValueOrDefault(def.RequiresUpgrade, 0) <= 0)
            {
                _popup.PopupEntity(Loc.GetString("shop-upgrade-locked"), uid, player);
                return;
            }
        }

        var cost = GetUpgradeLevelCost(def, currentLevel + 1);
        if (def.DiscountResearch is { } discountNode && _fsResearch.IsNodeUnlocked(discountNode))
            cost = (int)MathF.Round(cost * def.DiscountMultiplier);

        if (!_wallet.TryDeductCredits(mindId, cost))
        {
            _popup.PopupEntity(Loc.GetString("shop-weapon-insufficient-funds"), uid, player);
            return;
        }

        var newLevel = currentLevel + 1;
        var isFirstUpgradeEver = state.Levels.Count == 0;
        state.Levels[def.Id] = newLevel;
        state.TotalSpent += cost;
        _upgrades.ApplySingleUpgrade(weapon, player, def, newLevel);
        _researchStaticGrant.Reconcile(weapon);

        if (isFirstUpgradeEver)
            MarkAsUpgraded(weapon);

        _popup.PopupEntity(Loc.GetString("shop-upgrade-purchased", ("name", def.Name)), uid, player);
        var (upAcc, upNextAcc) = ComputeAccuracy(player, comp);
        SendWeaponLevels(mindId, CollectShopLevels(player, comp), ComputeWeaponTitle(player, comp), upAcc, upNextAcc);
    }

    // Every prototype this shop considers "its" weapon: the weapon itself, its aliases, and any
    // upgrade retarget. One search over all of them beats one search per prototype.
    private static HashSet<string> ShopProtoIds(FSShopWeaponComponent comp, bool includeUpgradeTargets = false)
    {
        var protos = new HashSet<string>();
        if (comp.WeaponProtoId is { } main)
            protos.Add(main.Id);
        foreach (var alias in comp.WeaponProtoIdAliases)
            protos.Add(alias.Id);

        if (!includeUpgradeTargets)
            return protos;

        foreach (var up in comp.Upgrades)
        {
            if (up.TargetWeaponProtoId is { } t)
                protos.Add(t.Id);
        }
        return protos;
    }

    private Dictionary<string, int> CollectShopLevels(EntityUid player, FSShopWeaponComponent comp)
    {
        var merged = new Dictionary<string, int>();
        if (comp.WeaponProtoId == null)
            return merged;

        var carried = new List<CarriedItem>();
        _search.Collect(player, ShopProtoIds(comp, includeUpgradeTargets: true), carried);

        foreach (var item in carried)
        {
            if (!TryComp<FSWeaponUpgradeStateComponent>(item.Uid, out var st))
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

        if (!TryAccess(shopUid, comp, player))
            return;

        var userId = mind.UserId.Value;

        var now = _timing.CurTime;
        if (_lastSellTime.TryGetValue(userId, out var lastSell)
            && (now - lastSell).TotalSeconds < SellCooldownSeconds)
            return;

        CleanRecentSells(now);
        var candidates = new List<CarriedItem>();
        _search.Collect(player, ShopProtoIds(comp), candidates, requireUpgradeState: true);
        if (candidates.Count == 0)
        {
            SendSellResponse(userId, success: false, "No copy of this weapon found in inventory.");
            return;
        }

        // Sell the least upgraded copy, so a player with two never loses the better one.
        candidates.Sort((a, b) =>
        {
            var aSum = TryComp<FSWeaponUpgradeStateComponent>(a.Uid, out var as_) ? as_.Levels.Values.Sum() : 0;
            var bSum = TryComp<FSWeaponUpgradeStateComponent>(b.Uid, out var bs_) ? bs_.Levels.Values.Sum() : 0;
            return aSum.CompareTo(bSum);
        });
        var weapon = candidates[0].Uid;

        if (_recentSells.ContainsKey(GetNetEntity(weapon)))
            return;

        var combinedSpent = TryComp<FSWeaponUpgradeStateComponent>(weapon, out var ws) ? ws.TotalSpent : 0;

        var baseRefund    = (int)(comp.Price * 0.40f);
        var upgradeRefund = (int)(combinedSpent * 0.40f);
        var totalRefund   = baseRefund + upgradeRefund;
        totalRefund = (int)(Math.Round(totalRefund / 50.0) * 50);
        totalRefund = Math.Max(0, totalRefund);

        // Record the sale before deleting. The dedup key is the net entity, because a raw
        // EntityUid is recycled and would block an unrelated later sale inside the window.
        _lastSellTime[userId] = now;
        _recentSells[GetNetEntity(weapon)] = now;

        QueueDel(weapon);
        CleanupAmmoForWeapon(player, comp);
        _wallet.GiveCredits(mindId, totalRefund);

        _popup.PopupEntity($"Sold for ${totalRefund:N0}.", shopUid, player);
        SendSellResponse(userId, success: true, "");

        SendWeaponLevels(mindId, new Dictionary<string, int>(), "");
    }

    private void CleanRecentSells(TimeSpan now)
    {
        var stale = new List<NetEntity>();
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

    private void TryGiveItemToPlayer(EntityUid player, EntityUid item)
    {
        // Grenade packs are pocket items — they must stay in inventory, never in hand.
        if (HasComp<FSGrenadePackComponent>(item))
        {
            _stash.Stash(player, item);
            return;
        }

        if (_hands.TryPickupAnyHand(player, item))
            return;

        _stash.Stash(player, item);
    }

    private void MarkAsUpgraded(EntityUid weapon)
    {
        var meta = MetaData(weapon);
        if (!meta.EntityName.EndsWith(UpgradedSuffix))
            _metaData.SetEntityName(weapon, meta.EntityName + UpgradedSuffix, meta);
    }

    private string ComputeWeaponTitle(EntityUid player, FSShopWeaponComponent comp)
    {
        if (comp.WeaponProtoId == null)
            return "";

        var matches = new List<CarriedItem>();
        _search.Collect(player, ShopProtoIds(comp), matches);
        if (matches.Count == 0)
            return "";

        var rawName = MetaData(matches[0].Uid).EntityName;
        var baseName = rawName.EndsWith(UpgradedSuffix)
            ? rawName[..^UpgradedSuffix.Length]
            : rawName;
        baseName = Capitalize(baseName);

        var label = CarryLabel(player, matches[0]);

        return matches.Count == 1
            ? $"{baseName} ({label})"
            : $"{baseName} No. 1 ({label})";
    }

    private string CarryLabel(EntityUid player, CarriedItem item)
    {
        switch (item.Kind)
        {
            case CarryKind.Hand:
                if (!TryComp<HandsComponent>(player, out var hands)
                    || !_hands.TryGetHand((player, hands), item.Where, out var hand))
                    return "Hand";
                return hand.Value.Location switch
                {
                    HandLocation.Left  => "Left Hand",
                    HandLocation.Right => "Right Hand",
                    _                  => "Hand",
                };
            case CarryKind.Equipped:
                return item.Where switch
                {
                    "belt"        => "Belt",
                    "suitstorage" => "Suit Storage",
                    "pocket1"     => "Pocket 1",
                    "pocket2"     => "Pocket 2",
                    _             => Capitalize(item.Where),
                };
            default:
                return "Backpack";
        }
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    private void CleanupAmmoForWeapon(EntityUid player, FSShopWeaponComponent shopComp)
    {
        var ammoProtos = new HashSet<string>();
        if (shopComp.StarterAmmoProtoId != null)
            ammoProtos.Add(shopComp.StarterAmmoProtoId.Value.Id);
        foreach (var upgrade in shopComp.Upgrades)
        {
            if (upgrade.Type == WeaponUpgradeType.SpawnItem && upgrade.SpawnProtoId != null)
                ammoProtos.Add(upgrade.SpawnProtoId.Value.Id);
        }
        if (ammoProtos.Count == 0)
            return;

        var toDelete = new List<CarriedItem>();
        _search.Collect(player, ammoProtos, toDelete);

        foreach (var item in toDelete)
        {
            if (Exists(item.Uid))
                QueueDel(item.Uid);
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

    private void SendWeaponLevels(EntityUid mindId, Dictionary<string, int> levels, string title = "",
        int accuracy = -1, Dictionary<string, int>? nextAccuracy = null)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            return;
        RaiseNetworkEvent(
            new UpgradeLevelsUpdatedEvent(new Dictionary<string, int>(levels), title, accuracy, nextAccuracy),
            Filter.SinglePlayer(session));
    }
}
