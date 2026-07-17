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

namespace Content.Server._FinalStand.Armor;

public sealed class FSArmorShopSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> ArmorTierItemTag = "FSArmorTierItem";
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    // mindId → purchased tier ID; persists across respawns
    private readonly Dictionary<EntityUid, string> _purchasedTier = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
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
        _ui.SetUiState(uid, FSArmorShopUiKey.Key, new FSArmorShopState(tierId, GetCredits(mindId)));
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

        var oldTier = oldId != null ? FSArmorShopDefs.GetTier(oldId) : null;
        var refund = oldTier != null ? oldTier.Price / 2 : 0;
        var netCost = tier.Price - refund;

        if (!TryComp<FSPlayerWalletComponent>(mindId, out var wallet) || wallet.Credits < netCost)
            return;

        if (refund > 0) _wallet.GiveCredits(mindId, refund);
        _wallet.TryDeductCredits(mindId, tier.Price);

        _purchasedTier[mindId] = tier.Id;
        ApplyArmorToMob(player, tier);

        _ui.SetUiState(uid, FSArmorShopUiKey.Key, new FSArmorShopState(tier.Id, GetCredits(mindId)));
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _)) return;
        if (!_purchasedTier.TryGetValue(mindId, out var tierId)) return;
        var tier = FSArmorShopDefs.GetTier(tierId);
        if (tier != null) ApplyArmorToMob(ev.Mob, tier);
    }

    private void ApplyArmorToMob(EntityUid mob, FSArmorTierDef tier)
    {
        // Remove existing FS armor tier item if present
        if (_inventory.TryGetSlotEntity(mob, "outerClothing", out var existing)
            && _tags.HasTag(existing.Value, ArmorTierItemTag))
        {
            _inventory.TryUnequip(mob, "outerClothing", silent: true, force: true);
            Del(existing.Value);
        }

        var item = Spawn(tier.SpawnId, Transform(mob).Coordinates);
        _inventory.TryEquip(mob, item, "outerClothing", silent: true, force: true);

        EnsureComp<FSPlayerArmorComponent>(mob).TierId = tier.Id;
    }

    private int GetCredits(EntityUid mindId) =>
        TryComp<FSPlayerWalletComponent>(mindId, out var w) ? w.Credits : 0;
}
