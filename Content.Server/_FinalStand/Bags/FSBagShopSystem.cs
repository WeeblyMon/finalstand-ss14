using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Bags;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Server.GameObjects;

namespace Content.Server._FinalStand.Bags;

public sealed class FSBagShopSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<FSBagShopComponent>(FSBagShopUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnShopOpened);
            subs.Event<FSBagShopBuyMessage>(OnBuy);
        });
    }

    private void OnShopOpened(EntityUid uid, FSBagShopComponent comp, BoundUIOpenedEvent args)
    {
        SendState(uid, comp, args.Actor);
    }

    private void OnBuy(EntityUid uid, FSBagShopComponent comp, FSBagShopBuyMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid() || !_mind.TryGetMind(player, out var mindId, out _))
            return;

        var entry = comp.Catalog.FirstOrDefault(e => e.Proto.Id == args.ProtoId);
        if (entry == null)
            return;

        if (!_wallet.TryDeductCredits(mindId, entry.Price))
        {
            _popup.PopupEntity(Loc.GetString("shop-weapon-insufficient-funds"), uid, player);
            return;
        }

        Spawn(entry.Proto, Transform(uid).Coordinates);
        _popup.PopupEntity(Loc.GetString("shop-weapon-purchased"), uid, player);
        SendState(uid, comp, player);
    }

    private void SendState(EntityUid uid, FSBagShopComponent comp, EntityUid player)
    {
        if (!_mind.TryGetMind(player, out var mindId, out _))
            return;

        var entries = comp.Catalog
            .Select(e => new FSBagShopEntryState(e.Proto.Id, e.Name, e.Description, e.Price))
            .ToList();

        _ui.ServerSendUiMessage(uid, FSBagShopUiKey.Key, new FSBagShopState(entries, _wallet.GetCredits(mindId)), player);
    }
}
