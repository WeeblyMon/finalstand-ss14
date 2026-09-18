using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Armor.Shop;
using Content.Shared._FinalStand.Economy;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Mind;
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

    private readonly Dictionary<EntityUid, string> _purchasedTier = new();

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

        _purchasedTier.TryGetValue(mindId, out var tierId);
        _ui.ServerSendUiMessage(uid, FSArmorShopUiKey.Key, new FSArmorShopState(tierId, _wallet.GetCredits(mindId)), player);
    }

    private void OnBuy(EntityUid uid, FSArmorShopComponent comp, FSArmorShopBuyMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid()) return;
        if (!_mind.TryGetMind(player, out var mindId, out _)) return;

        var tier = FSArmorShopDefs.GetTier(args.TierId);
        if (tier == null) return;

        _purchasedTier.TryGetValue(mindId, out var oldId);
        if (oldId == tier.Id) return;

        var netCost = FSArmorShopDefs.GetNetCost(oldId, tier);

        if (!_wallet.TryDeductCredits(mindId, netCost))
            return;

        if (!ApplyArmorToMob(player, tier))
        {
            _wallet.GiveCredits(mindId, netCost);
            return;
        }

        _purchasedTier[mindId] = tier.Id;
        _ui.ServerSendUiMessage(uid, FSArmorShopUiKey.Key, new FSArmorShopState(tier.Id, _wallet.GetCredits(mindId)), player);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _purchasedTier.Clear();
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _)) return;
        if (!_purchasedTier.TryGetValue(mindId, out var tierId)) return;
        var tier = FSArmorShopDefs.GetTier(tierId);
        if (tier != null) ApplyArmorToMob(ev.Mob, tier);
    }

    private bool ApplyArmorToMob(EntityUid mob, FSArmorTierDef tier)
    {
        if (_inventory.TryGetSlotEntity(mob, "outerClothing", out var existing))
        {
            if (!_inventory.TryUnequip(mob, "outerClothing", silent: true, force: true))
                return false;

            if (_tags.HasTag(existing.Value, ArmorTierItemTag))
                Del(existing.Value);
        }

        var item = Spawn(tier.SpawnId, Transform(mob).Coordinates);

        if (_inventory.TryEquip(mob, item, "outerClothing", silent: true, force: true))
            return true;

        Del(item);
        return false;
    }
}
