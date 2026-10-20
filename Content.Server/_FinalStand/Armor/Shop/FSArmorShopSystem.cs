using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Armor.Shop;
using Content.Shared._FinalStand.Economy;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Armor.Shop;

public sealed partial class FSArmorShopSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> ArmorTierItemTag = "FSArmorTierItem";
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private TagSystem _tags = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    private readonly Dictionary<EntityUid, ProtoId<FSArmorTierPrototype>> _purchasedTier = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        Subs.BuiEvents<FSArmorShopComponent>(FSArmorShopUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnShopOpened);
            subs.Event<FSArmorShopBuyMessage>(OnBuy);
        });
    }

    private void OnShopOpened(EntityUid uid, FSArmorShopComponent comp, BoundUIOpenedEvent args)
    {
        var player = args.Actor;
        if (!_mind.TryGetMind(player, out var mindId, out _)) return;

        ProtoId<FSArmorTierPrototype>? equipped = null;
        if (_purchasedTier.TryGetValue(mindId, out var tierId))
            equipped = tierId;

        _ui.ServerSendUiMessage(uid, FSArmorShopUiKey.Key, new FSArmorShopState(equipped, _wallet.GetCredits(mindId)), player);
    }

    private void OnBuy(EntityUid uid, FSArmorShopComponent comp, FSArmorShopBuyMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid()) return;
        if (!_mind.TryGetMind(player, out var mindId, out _)) return;

        if (args.Tier.Id is null || !_proto.TryIndex(args.Tier, out var tier))
            return;

        FSArmorTierPrototype? old = null;
        if (_purchasedTier.TryGetValue(mindId, out var oldId))
            _proto.TryIndex(oldId, out old);

        if (old == tier)
            return;

        // The trade-in only counts for the suit being handed back; one taken off and passed on earns nothing.
        var tradeIn = old != null && Wears(player, old) ? old : null;
        if (old != null && tradeIn == null)
            _popup.PopupEntity(Loc.GetString("fs-armor-shop-trade-in-missing"), uid, player);

        var netCost = FSArmorShopDefs.GetNetCost(tradeIn, tier);

        if (netCost > 0 && !_wallet.TryDeductCredits(mindId, netCost))
        {
            _popup.PopupEntity(Loc.GetString("fs-armor-shop-cant-afford", ("price", netCost)), uid, player);
            return;
        }

        if (!ApplyArmorToMob(player, tier))
        {
            if (netCost > 0)
                _wallet.GiveCredits(mindId, netCost);
            return;
        }

        // A step down refunds the difference.
        if (netCost < 0)
            _wallet.GiveCredits(mindId, -netCost);

        _purchasedTier[mindId] = tier.ID;
        _ui.ServerSendUiMessage(uid, FSArmorShopUiKey.Key, new FSArmorShopState(tier.ID, _wallet.GetCredits(mindId)), player);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _purchasedTier.Clear();
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _)) return;
        if (_purchasedTier.TryGetValue(mindId, out var tierId) && _proto.TryIndex(tierId, out var tier))
            ApplyArmorToMob(ev.Mob, tier);
    }

    private bool Wears(EntityUid mob, FSArmorTierPrototype tier)
    {
        return _inventory.TryGetSlotEntity(mob, "outerClothing", out var worn)
               && MetaData(worn.Value).EntityPrototype?.ID == tier.Spawn.Id;
    }

    private bool ApplyArmorToMob(EntityUid mob, FSArmorTierPrototype tier)
    {
        if (_inventory.TryGetSlotEntity(mob, "outerClothing", out var existing))
        {
            if (!_inventory.TryUnequip(mob, "outerClothing", silent: true, force: true))
                return false;

            if (_tags.HasTag(existing.Value, ArmorTierItemTag))
                Del(existing.Value);
        }

        var item = Spawn(tier.Spawn, Transform(mob).Coordinates);

        if (_inventory.TryEquip(mob, item, "outerClothing", silent: true, force: true))
            return true;

        Del(item);
        return false;
    }
}
