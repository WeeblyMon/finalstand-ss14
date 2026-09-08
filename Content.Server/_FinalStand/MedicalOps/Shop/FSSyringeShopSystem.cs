using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.MedicalOps.Shop;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Robust.Server.GameObjects;

namespace Content.Server._FinalStand.MedicalOps.Shop;

public sealed class FSSyringeShopSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    private readonly Dictionary<EntityUid, string> _ownedTier = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);

        Subs.BuiEvents<FSSyringeShopComponent>(FSSyringeShopUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnShopOpened);
            subs.Event<FSSyringeShopBuyMessage>(OnBuy);
        });
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _ownedTier.Clear();
    }

    private void OnShopOpened(EntityUid uid, FSSyringeShopComponent comp, BoundUIOpenedEvent args)
    {
        if (!_mind.TryGetMind(args.Actor, out var mindId, out _))
            return;

        SendState(uid, mindId, args.Actor);
    }

    private void OnBuy(EntityUid uid, FSSyringeShopComponent comp, FSSyringeShopBuyMessage args)
    {
        if (!args.Actor.IsValid() || !_mind.TryGetMind(args.Actor, out var mindId, out _))
            return;

        if (FSSyringeShopDefs.GetTier(args.TierId) is not { } tier)
            return;

        _ownedTier.TryGetValue(mindId, out var ownedId);

        if (FSSyringeShopDefs.RankOf(ownedId) >= tier.Rank)
            return;

        if (!_wallet.TryDeductCredits(mindId, tier.Price))
            return;

        if (!Give(args.Actor, tier))
        {
            _wallet.GiveCredits(mindId, tier.Price);
            return;
        }

        _ownedTier[mindId] = tier.Id;
        SendState(uid, mindId, args.Actor);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_mind.TryGetMind(ev.Mob, out var mindId, out _))
            return;

        if (!_ownedTier.TryGetValue(mindId, out var tierId))
            return;

        if (FSSyringeShopDefs.GetTier(tierId) is { } tier)
            Give(ev.Mob, tier);
    }

    private bool Give(EntityUid mob, FSSyringeTierDef tier)
    {
        var item = Spawn(tier.SpawnId, Transform(mob).Coordinates);

        if (_hands.TryPickupAnyHand(mob, item))
            return true;

        return !Deleted(item);
    }

    private void SendState(EntityUid uid, EntityUid mindId, EntityUid actor)
    {
        _ownedTier.TryGetValue(mindId, out var ownedId);

        _ui.ServerSendUiMessage(uid,
            FSSyringeShopUiKey.Key,
            new FSSyringeShopState(ownedId, _wallet.GetCredits(mindId)),
            actor);
    }
}
