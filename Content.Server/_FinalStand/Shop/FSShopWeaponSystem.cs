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
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

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

    /// <summary>Finds the held weapon and pushes its levels + title to the client.</summary>
    private void SendCurrentLevels(EntityUid uid, FSShopWeaponComponent comp, EntityUid player)
    {
        if (!_mind.TryGetMind(player, out var mindId, out _))
            return;

        var weapon = FindHeldWeapon(player, comp.WeaponProtoId);
        var levels = (weapon != null && TryComp<FSWeaponUpgradeStateComponent>(weapon.Value, out var state))
            ? state.Levels
            : new Dictionary<string, int>();

        var title = ComputeWeaponTitle(player, comp.WeaponProtoId);
        SendWeaponLevels(mindId, levels, title);
    }

    private void OnBuyMessage(EntityUid uid, FSShopWeaponComponent comp, FSShopBuyMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid())
            return;

        if (!_mind.TryGetMind(player, out var mindId, out _))
            return;

        if (!_wallet.TryDeductCredits(mindId, comp.Price))
        {
            _popup.PopupEntity(Loc.GetString("shop-weapon-insufficient-funds"), uid, player);
            return;
        }

        var weapon = Spawn(comp.WeaponProtoId, Transform(player).Coordinates);
        TryGiveItemToPlayer(player, weapon);
        _popup.PopupEntity(Loc.GetString("shop-weapon-purchased"), uid, player);
        // Fresh weapon — send empty levels and updated title.
        var title = ComputeWeaponTitle(player, comp.WeaponProtoId);
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

        var weapon = FindHeldWeapon(player, comp.WeaponProtoId);
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

        var cost = def.BaseCost * (currentLevel + 1);
        if (!_wallet.TryDeductCredits(mindId, cost))
        {
            _popup.PopupEntity(Loc.GetString("shop-weapon-insufficient-funds"), uid, player);
            return;
        }

        var newLevel = currentLevel + 1;
        var isFirstUpgradeEver = state.Levels.Count == 0;
        state.Levels[def.Id] = newLevel;
        _upgrades.ApplySingleUpgrade(weapon.Value, player, def, newLevel);

        // Add "(Upgraded)" suffix to the entity name on the very first upgrade.
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
                // Fresh akimbo spawn — the partner is a clean prototype. Re-apply ALL
                // previously purchased upgrades so it matches the primary gun.
                foreach (var prevDef in comp.Upgrades)
                {
                    if (prevDef.Id == def.Id) continue; // skip Akimbo itself
                    if (!state.Levels.TryGetValue(prevDef.Id, out var prevLevel) || prevLevel == 0)
                        continue;
                    pairedState.Levels[prevDef.Id] = prevLevel;
                    for (var lvl = 1; lvl <= prevLevel; lvl++)
                        _upgrades.ApplySingleUpgrade(paired, player, prevDef, lvl, spawnItems: false);
                }
            }
            else
            {
                // Normal case: mirror just the current upgrade level delta.
                pairedState.Levels[def.Id] = newLevel;
                _upgrades.ApplySingleUpgrade(paired, player, def, newLevel, spawnItems: false);
            }

            MarkAsUpgraded(paired); // idempotent — guard inside prevents duplicate suffix
        }

        _popup.PopupEntity(Loc.GetString("shop-upgrade-purchased", ("name", def.Name)), uid, player);
        var title = ComputeWeaponTitle(player, comp.WeaponProtoId);
        SendWeaponLevels(mindId, state.Levels, title);
    }

    // ---- Helpers ----

    private static readonly string[] InventorySlotPriority = ["belt", "suitstorage", "pocket1", "pocket2"];

    private void TryGiveItemToPlayer(EntityUid player, EntityUid item)
    {
        if (_hands.TryPickupAnyHand(player, item))
            return;

        foreach (var slot in InventorySlotPriority)
        {
            if (_inventory.TryEquip(player, item, slot, silent: true))
                return;
        }

        if (_inventory.TryGetSlotEntity(player, "back", out var backpack))
            _storage.Insert(backpack.Value, item, out _, user: player, playSound: false);

        // falls to floor at player coords if everything fails
    }

    private void MarkAsUpgraded(EntityUid weapon)
    {
        var meta = MetaData(weapon);
        if (!meta.EntityName.EndsWith(" (Upgraded)"))
            _metaData.SetEntityName(weapon, meta.EntityName + " (Upgraded)", meta);
    }

    /// <summary>
    ///     Builds a display title for the held weapon, e.g. "Viper (Right Hand)"
    ///     or "Viper No. 2 (Left Hand)" when two copies are held (akimbo).
    /// </summary>
    private string ComputeWeaponTitle(EntityUid player, EntProtoId protoId)
    {
        if (!TryComp<HandsComponent>(player, out var hands))
            return "";

        // Collect all matching held weapons in hand order.
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

        // Weapon name from metadata (strip any existing "(Upgraded)" for the title).
        var rawName = MetaData(matches[0].uid).EntityName;
        var baseName = rawName.EndsWith(" (Upgraded)")
            ? rawName[..^" (Upgraded)".Length]
            : rawName;
        baseName = Capitalize(baseName);

        // Determine which hand the first match is in.
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

    /// <summary>Returns the first held entity whose prototype matches <paramref name="protoId"/>.</summary>
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
