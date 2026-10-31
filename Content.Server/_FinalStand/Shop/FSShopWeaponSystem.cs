using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Research;
using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Deployables;
using Content.Shared._FinalStand.Deployables;
using Content.Server._FinalStand.Science;
using Content.Server.Popups;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
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
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSPlayerUpgradesSystem _upgrades = default!;
    [Dependency] private FSItemStashSystem _stash = default!;
    [Dependency] private FSInventorySearchSystem _search = default!;
    [Dependency] private FSResearchSystem _fsResearch = default!;
    [Dependency] private FSMedicalResearchSystem _medicalResearch = default!;
    [Dependency] private MedicalOps.FSMedicalRosterSystem _roster = default!;
    [Dependency] private FSResearchStaticGrantSystem _researchStaticGrant = default!;
    [Dependency] private FSScienceOnlySystem _science = default!;
    [Dependency] private FSDepartmentAccessSystem _engineering = default!;
    [Dependency] private FSDeployableSystem _deployable = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private FSShopBindingSystem _binding = default!;
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;

    private const double SellCooldownSeconds = 2.0;
    private const double SellDedupWindowSeconds = 5.0;
    private string UpgradedSuffix => " " + Loc.GetString("fs-weapon-shop-upgraded-suffix");

    private readonly Dictionary<NetUserId, TimeSpan> _lastSellTime = new();
    private readonly Dictionary<NetEntity, TimeSpan> _recentSells = new();
    private readonly List<CarriedItem> _prerequisiteCandidates = new();

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
        if (comp.Purchasable)
            args.PushMarkup(Loc.GetString("shop-weapon-examine-price", ("price", comp.Price)));
    }

    // Research and department locks. Every entry point checks this, not just the UI open.
    private bool TryAccess(EntityUid uid, FSShopWeaponComponent comp, EntityUid player, bool silent = false)
    {
        if (comp.RequiresResearch is { } required && !IsResearched(required))
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

        if (comp.RequiresEngineering && !_engineering.IsEngineering(player))
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString("shop-weapon-locked-engineering"), uid, player);
            return false;
        }

        if (comp.RequiresMedical && !_roster.IsMedical(player))
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString("fs-medical-only"), uid, player);
            return false;
        }

        if (comp.RequiresHeldWeapon && !HoldsShopWeapon(player, comp))
        {
            if (!silent)
                _popup.PopupEntity(Loc.GetString("shop-weapon-locked-held"), uid, player);
            return false;
        }

        return true;
    }

    private bool HoldsShopWeapon(EntityUid player, FSShopWeaponComponent comp)
    {
        var protos = ShopProtoIds(comp);
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (MetaData(held).EntityPrototype?.ID is { } id && protos.Contains(id))
                return true;
        }

        return false;
    }

    private bool IsResearched(string nodeId) =>
        _fsResearch.IsNodeUnlocked(nodeId) || _medicalResearch.IsNodeUnlocked(nodeId);

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

        if (!TryAccess(uid, comp, player) || !comp.Purchasable)
            return;

        var owned = new List<CarriedItem>();
        _search.Collect(player, ShopProtoIds(comp), owned);

        if (owned.Any(c => HasComp<FSGrenadePackComponent>(c.Uid)))
        {
            _popup.PopupEntity(Loc.GetString("shop-grenade-already-owned"), uid, player);
            return;
        }

        if (comp.SinglePurchase && owned.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("shop-item-already-owned"), uid, player);
            return;
        }

        var bindable = IsBindable(comp);
        if (bindable && _binding.OwnsAny(mindId, ShopProtoIds(comp)))
        {
            _popup.PopupEntity(Loc.GetString("shop-item-already-owned-elsewhere"), uid, player);
            return;
        }

        if (!_wallet.TryDeductCredits(mindId, comp.Price))
        {
            _popup.PopupEntity(Loc.GetString("shop-weapon-insufficient-funds"), uid, player);
            return;
        }

        var weapon = GrantWeapon(player, comp)!.Value;
        if (bindable)
            _binding.Bind(weapon, mindId);

        var starters = comp.Upgrades.Any(def => def.Starter);

        _adminLogger.Add(LogType.StorePurchase, LogImpact.Low,
            $"{ToPrettyString(player):player} bought {ToPrettyString(weapon):item} for {comp.Price} credits at {ToPrettyString(uid):store}");

        _popup.PopupEntity(Loc.GetString("shop-weapon-purchased"), uid, player);
        var (buyAcc, buyNextAcc) = ComputeAccuracy(player, comp);
        SendWeaponLevels(mindId, starters ? CollectShopLevels(player, comp) : new Dictionary<string, int>(), ComputeWeaponTitle(player, comp), buyAcc, buyNextAcc);
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

        CarriedItem? found = null;
        var carried = new List<CarriedItem>();
        _search.Collect(player, targetIds, carried);
        foreach (var item in carried)
        {
            if (!_binding.IsUsableBy(item.Uid, player))
                continue;

            found = item;
            break;
        }

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

        if (def.RequiresResearch is { } requiredNode && !IsResearched(requiredNode))
        {
            var nodeName = _protoManager.TryIndex(requiredNode, out var node) ? node.Name : requiredNode.Id;
            _popup.PopupEntity(Loc.GetString("shop-upgrade-locked-research", ("node", nodeName)), uid, player);
            return;
        }

        // The prerequisite lives on the shop's own weapon (the cutlass unlocks flintlock upgrades), and has to be
        // on a copy this player may use, not one bound to someone else.
        if (def.RequiresUpgrade is { } required && !HasUsableUpgrade(player, ShopProtoIds(comp), required))
        {
            _popup.PopupEntity(Loc.GetString("shop-upgrade-locked"), uid, player);
            return;
        }

        var cost = def.LevelCost(currentLevel + 1,
            def.DiscountResearch is { } discountNode && _fsResearch.IsNodeUnlocked(discountNode));

        if (!_wallet.TryDeductCredits(mindId, cost))
        {
            _popup.PopupEntity(Loc.GetString("shop-weapon-insufficient-funds"), uid, player);
            return;
        }

        if (def.Type == WeaponUpgradeType.ConvertWeapon && def.SpawnProtoId is { } result)
        {
            _adminLogger.Add(LogType.StorePurchase, LogImpact.Low,
                $"{ToPrettyString(player):player} bought upgrade {def.Id} for {cost} credits, converting {ToPrettyString(weapon):item} into {result}");

            ConvertWeapon(player, weapon, state, result, def.Id, cost);
            _popup.PopupEntity(Loc.GetString("shop-upgrade-purchased", ("name", def.Name)), uid, player);
            var (convAcc, convNextAcc) = ComputeAccuracy(player, comp);
            SendWeaponLevels(mindId, CollectShopLevels(player, comp), ComputeWeaponTitle(player, comp), convAcc, convNextAcc);
            return;
        }

        var newLevel = currentLevel + 1;
        var isFirstUpgradeEver = state.Levels.Count == 0;
        state.Levels[def.Id] = newLevel;
        state.TotalSpent += cost;
        weapon = _upgrades.ApplySingleUpgrade(weapon, player, def, newLevel);
        _researchStaticGrant.Reconcile(weapon);

        _adminLogger.Add(LogType.StorePurchase, LogImpact.Low,
            $"{ToPrettyString(player):player} bought upgrade {def.Id} level {newLevel} on {ToPrettyString(weapon):item} for {cost} credits");

        if (isFirstUpgradeEver)
            MarkAsUpgraded(weapon);

        _popup.PopupEntity(Loc.GetString("shop-upgrade-purchased", ("name", def.Name)), uid, player);
        var (upAcc, upNextAcc) = ComputeAccuracy(player, comp);
        SendWeaponLevels(mindId, CollectShopLevels(player, comp), ComputeWeaponTitle(player, comp), upAcc, upNextAcc);
    }

    private void ConvertWeapon(EntityUid player, EntityUid weapon, FSWeaponUpgradeStateComponent state,
        EntProtoId result, string upgradeId, int cost)
    {
        var converted = Spawn(result, Transform(player).Coordinates);
        var convertedState = EnsureComp<FSWeaponUpgradeStateComponent>(converted);
        _upgrades.CopyUpgradeState(state, convertedState);
        convertedState.Levels[upgradeId] = 1;
        convertedState.TotalSpent = state.TotalSpent + cost;
        Dirty(converted, convertedState);

        if (TryComp<FSShopBoundComponent>(weapon, out var bound))
            _binding.Bind(converted, bound.OwnerMind);

        // Gone at once: a second convert message this tick would still find the old weapon, at level 0, and get
        // another fully upgraded copy. It also frees the hand for the new one.
        Del(weapon);
        TryGiveItemToPlayer(player, converted);
        _researchStaticGrant.Reconcile(converted);
    }

    private bool IsBindable(FSShopWeaponComponent comp)
    {
        if (comp.SinglePurchase)
            return true;

        return comp.WeaponProtoId is { } id
            && _protoManager.TryIndex(id, out var proto)
            && (proto.HasComp<FSGrenadePackComponent>(EntityManager.ComponentFactory)
                || proto.HasComp<FSDeployableItemComponent>(EntityManager.ComponentFactory));
    }

    // Every prototype this shop considers "its" weapon: the weapon, its aliases, and any upgrade retarget.
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
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            if (!_binding.IsUsableBy(candidates[i].Uid, player))
                candidates.RemoveAt(i);
        }

        if (candidates.Count == 0)
        {
            SendSellResponse(userId, success: false, Loc.GetString("fs-weapon-shop-sell-none-found"));
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

        // Dedup key is the net entity, not the raw EntityUid, which gets recycled.
        _lastSellTime[userId] = now;
        _recentSells[GetNetEntity(weapon)] = now;

        if (TryComp<FSDeployableItemComponent>(weapon, out var deployable))
            _deployable.ClearDeployed(mindId, deployable.DeployedProtoId);

        _adminLogger.Add(LogType.StoreRefund, LogImpact.Low,
            $"{ToPrettyString(player):player} sold {ToPrettyString(weapon):item} for {totalRefund} credits at {ToPrettyString(shopUid):store}");

        QueueDel(weapon);
        CleanupAmmoForWeapon(player, comp, ws);
        _wallet.AddCredits(mindId, totalRefund);

        _popup.PopupEntity(Loc.GetString("fs-weapon-shop-sold-for", ("amount", totalRefund.ToString("N0"))), shopUid, player);
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

    public EntityUid? GrantWeapon(EntityUid player, FSShopWeaponComponent comp)
    {
        if (comp.WeaponProtoId is not { } proto)
            return null;

        var weapon = Spawn(proto, Transform(player).Coordinates);
        EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
        TryGiveItemToPlayer(player, weapon);

        foreach (var def in comp.Upgrades)
        {
            if (!def.Starter)
                continue;

            EnsureComp<FSWeaponUpgradeStateComponent>(weapon).Levels[def.Id] = 1;
            _upgrades.ApplySingleUpgrade(weapon, player, def, 1);
        }

        if (comp.StarterAmmoProtoId is { } ammoProto)
        {
            var coords = Transform(player).Coordinates.Offset(new System.Numerics.Vector2(0.5f, 0.5f));
            for (var i = 0; i < comp.StarterAmmoCount; i++)
                _stash.Stash(player, Spawn(ammoProto, coords));
        }

        return weapon;
    }

    public void TryGiveItemToPlayer(EntityUid player, EntityUid item)
    {
        // Grenade packs are pocket items; they must stay in inventory, never in hand.
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
        var suffix = UpgradedSuffix;
        if (!meta.EntityName.EndsWith(suffix))
            _metaData.SetEntityName(weapon, meta.EntityName + suffix, meta);
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
        var suffix = UpgradedSuffix;
        var baseName = rawName.EndsWith(suffix)
            ? rawName[..^suffix.Length]
            : rawName;
        baseName = Capitalize(baseName);

        var label = CarryLabel(player, matches[0]);

        return matches.Count == 1
            ? Loc.GetString("fs-weapon-shop-title", ("name", baseName), ("where", label))
            : Loc.GetString("fs-weapon-shop-title-first", ("name", baseName), ("where", label));
    }

    private string CarryLabel(EntityUid player, CarriedItem item)
    {
        switch (item.Kind)
        {
            case CarryKind.Hand:
                if (!TryComp<HandsComponent>(player, out var hands)
                    || !_hands.TryGetHand((player, hands), item.Where, out var hand))
                    return Loc.GetString("fs-weapon-shop-carry-hand");
                return hand.Value.Location switch
                {
                    HandLocation.Left  => Loc.GetString("fs-weapon-shop-carry-left-hand"),
                    HandLocation.Right => Loc.GetString("fs-weapon-shop-carry-right-hand"),
                    _                  => Loc.GetString("fs-weapon-shop-carry-hand"),
                };
            default:
                return item.Where switch
                {
                    "belt"        => Loc.GetString("fs-weapon-shop-carry-belt"),
                    "suitstorage" => Loc.GetString("fs-weapon-shop-carry-suit-storage"),
                    "pocket1"     => Loc.GetString("fs-weapon-shop-carry-pocket", ("number", 1)),
                    "pocket2"     => Loc.GetString("fs-weapon-shop-carry-pocket", ("number", 2)),
                    "shoes"       => Loc.GetString("fs-weapon-shop-carry-boots"),
                    "back"        => Loc.GetString("fs-weapon-shop-carry-backpack"),
                    _             => Capitalize(item.Where),
                };
        }
    }

    private static string Capitalize(string s) =>
        s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    // Takes back what this copy came with, not every matching item: another copy or gun may share the ammo.
    private void CleanupAmmoForWeapon(EntityUid player, FSShopWeaponComponent shopComp, FSWeaponUpgradeStateComponent? state)
    {
        var budget = new Dictionary<string, int>();
        if (shopComp.StarterAmmoProtoId is { } starter)
            budget[starter.Id] = shopComp.StarterAmmoCount;

        foreach (var upgrade in shopComp.Upgrades)
        {
            if (upgrade.Type != WeaponUpgradeType.SpawnItem || upgrade.SpawnProtoId is not { } spawned)
                continue;

            var level = state?.Levels.GetValueOrDefault(upgrade.Id) ?? 0;
            budget[spawned.Id] = budget.GetValueOrDefault(spawned.Id) + level * upgrade.SpawnCountPerLevel;
        }

        if (budget.Count == 0)
            return;

        var carried = new List<CarriedItem>();
        _search.Collect(player, budget.Keys.ToHashSet(), carried);

        foreach (var item in carried)
        {
            var proto = MetaData(item.Uid).EntityPrototype?.ID;
            if (proto == null || budget.GetValueOrDefault(proto) <= 0)
                continue;

            budget[proto]--;
            QueueDel(item.Uid);
        }
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

    private bool HasUsableUpgrade(EntityUid player, IReadOnlySet<string> protoIds, string upgradeId)
    {
        _prerequisiteCandidates.Clear();
        _search.Collect(player, protoIds, _prerequisiteCandidates);
        foreach (var item in _prerequisiteCandidates)
        {
            if (_binding.IsUsableBy(item.Uid, player)
                && TryComp<FSWeaponUpgradeStateComponent>(item.Uid, out var state)
                && state.Levels.GetValueOrDefault(upgradeId, 0) > 0)
                return true;
        }

        return false;
    }
}
