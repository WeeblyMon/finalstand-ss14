// Job weapons the shop buys back are issued once per player per round, so rejoining can't farm them.
using Content.Shared._FinalStand.Shop;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Shop;

public sealed class FSStartingWeaponLimitSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private FSInventorySearchSystem _search = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private readonly HashSet<NetUserId> _issued = new();
    private readonly List<CarriedItem> _found = new();
    private HashSet<string>? _sellable;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _issued.Clear());
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _sellable = null);
    }

    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (_issued.Add(ev.Player.UserId))
            return;

        _found.Clear();
        _search.Collect(ev.Mob, GetSellable(), _found);
        if (_found.Count == 0)
            return;

        foreach (var item in _found)
            QueueDel(item.Uid);

        _popup.PopupEntity(Loc.GetString("fs-starting-weapon-already-issued"), ev.Mob, ev.Mob);
    }

    private HashSet<string> GetSellable()
    {
        if (_sellable != null)
            return _sellable;

        _sellable = new HashSet<string>();
        var name = Factory.GetComponentName<FSShopWeaponComponent>();
        foreach (var proto in _proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (!proto.TryGetComponent<FSShopWeaponComponent>(name, out var shop))
                continue;

            if (shop.WeaponProtoId is { } main)
                _sellable.Add(main.Id);

            foreach (var alias in shop.WeaponProtoIdAliases)
                _sellable.Add(alias.Id);
        }

        return _sellable;
    }
}
