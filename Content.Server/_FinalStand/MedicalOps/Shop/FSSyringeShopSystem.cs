using Content.Server._FinalStand.Economy;
using Content.Server.Chemistry.Components;
using Content.Shared._FinalStand.MedicalOps.Shop;
using Content.Shared.Chemistry;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Popups;

namespace Content.Server._FinalStand.MedicalOps.Shop;

public sealed class FSSyringeShopSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly Dictionary<EntityUid, string> _ownedTier = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        Subs.BuiEvents<ChemMasterComponent>(ChemMasterUiKey.Key, subs =>
        {
            subs.Event<FSSyringeShopBuyMessage>(OnBuy);
        });
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _ownedTier.Clear();
    }

    private void OnBuy(EntityUid uid, ChemMasterComponent comp, FSSyringeShopBuyMessage args)
    {
        if (!args.Actor.IsValid() || !_mind.TryGetMind(args.Actor, out var mindId, out _))
            return;

        if (FSSyringeShopDefs.GetTier(args.TierId) is not { } tier)
            return;

        _ownedTier.TryGetValue(mindId, out var ownedId);

        if (FSSyringeShopDefs.RankOf(ownedId) >= tier.Rank)
            return;

        if (!_wallet.TryDeductCredits(mindId, tier.Price))
        {
            _popup.PopupEntity(Loc.GetString("fs-syringe-shop-poor"), args.Actor, args.Actor);
            return;
        }

        if (!Give(args.Actor, tier))
        {
            _wallet.GiveCredits(mindId, tier.Price);
            return;
        }

        _ownedTier[mindId] = tier.Id;
        Sync(args.Actor, tier.Id);

        _popup.PopupEntity(Loc.GetString("fs-syringe-shop-bought", ("tier", tier.Name)), args.Actor, args.Actor);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _))
            return;

        if (!_ownedTier.TryGetValue(mindId, out var tierId))
            return;

        if (FSSyringeShopDefs.GetTier(tierId) is not { } tier)
            return;

        Give(ev.Mob, tier);
        Sync(ev.Mob, tier.Id);
    }

    private void Sync(EntityUid mob, string tierId)
    {
        var comp = EnsureComp<FSSyringeUpgradeComponent>(mob);
        comp.TierId = tierId;
        Dirty(mob, comp);
    }

    private bool Give(EntityUid mob, FSSyringeTierDef tier)
    {
        var item = Spawn(tier.SpawnId, Transform(mob).Coordinates);

        if (_hands.TryPickupAnyHand(mob, item))
            return true;

        return !Deleted(item);
    }
}
