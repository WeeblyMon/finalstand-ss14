using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Shop;

public sealed class FSShopClientSystem : EntitySystem
{
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    private static readonly ProtoId<ShaderPrototype> ShaderAffordable = "FSShopGlowAffordable";
    private static readonly ProtoId<ShaderPrototype> ShaderUnaffordable = "FSShopGlowUnaffordable";
    private static readonly ProtoId<ShaderPrototype> ShaderOwned = "FSShopGlowOwned";

    private enum ShopGlowState { Unaffordable, Affordable, Owned }

    public int CurrentCredits { get; private set; }
    public Dictionary<string, int> UpgradeLevels { get; private set; } = [];
    public string WeaponTitle { get; private set; } = "";

    public event Action? CreditsChanged;
    public event Action? UpgradeLevelsChanged;
    public event Action? RefreshNeeded;
    public event Action? PerkStateChanged;
    public event Action? SellCompleted;
    public event Action<string>? SellFailed;

    private readonly Dictionary<EntityUid, ShopGlowState> _lastGlowState = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnWalletUpdate);
        SubscribeNetworkEvent<UpgradeLevelsUpdatedEvent>(OnUpgradesUpdated);
        SubscribeNetworkEvent<PerkAddedEvent>(OnPerkAdded);
        SubscribeNetworkEvent<PerkRemovedAllEvent>(OnPerkRemovedAll);
        SubscribeNetworkEvent<FSShopSellCompletedEvent>(OnSellCompleted);
        SubscribeNetworkEvent<FSShopSellFailedEvent>(OnSellFailed);
        SubscribeLocalEvent<HandSelectedEvent>(OnHandSelected);
        SubscribeLocalEvent<HandDeselectedEvent>(OnHandDeselected);
        SubscribeLocalEvent<DidEquipHandEvent>(OnDidEquipHand);
        SubscribeLocalEvent<DidUnequipHandEvent>(OnDidUnequipHand);
        _client.PlayerJoinedServer += OnJoined;
        _client.PlayerLeaveServer += OnLeft;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _client.PlayerJoinedServer -= OnJoined;
        _client.PlayerLeaveServer -= OnLeft;
        ClearAllShaders();
    }

    public override void FrameUpdate(float frameTime)
    {
        var player = _player.LocalSession?.AttachedEntity;
        var query = EntityQueryEnumerator<FSShopWeaponComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var shop, out var sprite))
        {
            ShopGlowState state;
            if (player != null && PlayerHasWeapon(player.Value, shop.WeaponProtoId))
                state = ShopGlowState.Owned;
            else if (CurrentCredits >= shop.Price)
                state = ShopGlowState.Affordable;
            else
                state = ShopGlowState.Unaffordable;

            if (_lastGlowState.TryGetValue(uid, out var last) && last == state)
                continue;

            _lastGlowState[uid] = state;
            ApplyOutline(sprite, state);
        }
    }

    private void OnHandSelected(HandSelectedEvent ev)
    {
        if (_player.LocalSession?.AttachedEntity != ev.User) return;
        _lastGlowState.Clear();
        RefreshNeeded?.Invoke();
    }

    private void OnHandDeselected(HandDeselectedEvent ev)
    {
        if (_player.LocalSession?.AttachedEntity != ev.User) return;
        _lastGlowState.Clear();
        RefreshNeeded?.Invoke();
    }

    private void OnDidEquipHand(DidEquipHandEvent ev)
    {
        if (_player.LocalSession?.AttachedEntity != ev.User) return;
        _lastGlowState.Clear();
        RefreshNeeded?.Invoke();
    }

    private void OnDidUnequipHand(DidUnequipHandEvent ev)
    {
        if (_player.LocalSession?.AttachedEntity != ev.User) return;
        _lastGlowState.Clear();
        RefreshNeeded?.Invoke();
    }

    private void OnWalletUpdate(WalletUpdatedEvent ev)
    {
        CurrentCredits = ev.Credits;
        _lastGlowState.Clear();
        CreditsChanged?.Invoke();
    }

    private void OnUpgradesUpdated(UpgradeLevelsUpdatedEvent ev)
    {
        UpgradeLevels = ev.Levels;
        WeaponTitle = ev.WeaponTitle;
        UpgradeLevelsChanged?.Invoke();
    }

    private void OnPerkAdded(PerkAddedEvent ev)
    {
        // Only fire for the local player so other players' purchases don't trigger a refresh
        var localEntity = _player.LocalSession?.AttachedEntity;
        if (localEntity == null)
            return;
        PerkStateChanged?.Invoke();
    }

    private void OnPerkRemovedAll(PerkRemovedAllEvent ev)
    {
        var localEntity = _player.LocalSession?.AttachedEntity;
        if (localEntity == null)
            return;
        PerkStateChanged?.Invoke();
    }

    private void OnJoined(object? _, PlayerEventArgs __)
    {
        CurrentCredits = 0;
        UpgradeLevels = [];
        WeaponTitle = "";
        _lastGlowState.Clear();
        CreditsChanged?.Invoke();
    }

    private void OnLeft(object? _, PlayerEventArgs __)
    {
        CurrentCredits = 0;
        UpgradeLevels = [];
        WeaponTitle = "";
        ClearAllShaders();
        CreditsChanged?.Invoke();
    }

    public EntityUid? GetActiveGun()
    {
        var player = _player.LocalSession?.AttachedEntity;
        if (player == null || !TryComp<HandsComponent>(player.Value, out var hands))
            return null;
        if (hands.ActiveHandId == null)
            return null;
        _hands.TryGetHeldItem((player.Value, hands), hands.ActiveHandId, out var held);
        if (held == null || !HasComp<GunComponent>(held.Value))
            return null;
        return held.Value;
    }

    // Finds the owned instance of the weapon/grenade-pack matching protoId across hands and inventory.
    public EntityUid? FindOwnedWeapon(EntProtoId? protoId)
    {
        var player = _player.LocalSession?.AttachedEntity;
        if (player == null || protoId == null) return null;
        var targetId = protoId.Value.Id;

        foreach (var held in _hands.EnumerateHeld(player.Value))
        {
            if (MetaData(held).EntityPrototype?.ID == targetId)
                return held;
        }

        foreach (var slot in InventorySlots)
        {
            if (_inventory.TryGetSlotEntity(player.Value, slot, out var item) && item != null
                && MetaData(item.Value).EntityPrototype?.ID == targetId)
                return item.Value;
        }

        if (_inventory.TryGetSlotEntity(player.Value, "back", out var back) && back != null
            && TryComp<ContainerManagerComponent>(back.Value, out var mgr))
        {
            foreach (var container in mgr.Containers.Values)
            {
                foreach (var entity in container.ContainedEntities)
                {
                    if (MetaData(entity).EntityPrototype?.ID == targetId)
                        return entity;
                }
            }
        }

        return null;
    }

    private void OnSellCompleted(FSShopSellCompletedEvent _) => SellCompleted?.Invoke();

    private void OnSellFailed(FSShopSellFailedEvent ev) => SellFailed?.Invoke(ev.Reason);

    public EntityUid? GetLocalPlayer() => _player.LocalSession?.AttachedEntity;

    private static readonly string[] InventorySlots = ["belt", "suitstorage", "pocket1", "pocket2"];

    public bool PlayerHasWeaponInInventory(EntityUid? player, EntProtoId? protoId)
    {
        if (player == null || protoId == null) return false;
        var targetId = protoId.Value.Id;

        foreach (var held in _hands.EnumerateHeld(player.Value))
        {
            if (MetaData(held).EntityPrototype?.ID == targetId)
                return true;
        }

        foreach (var slot in InventorySlots)
        {
            if (_inventory.TryGetSlotEntity(player.Value, slot, out var item) && item != null
                && MetaData(item.Value).EntityPrototype?.ID == targetId)
                return true;
        }

        if (_inventory.TryGetSlotEntity(player.Value, "back", out var back) && back != null
            && TryComp<ContainerManagerComponent>(back.Value, out var mgr))
        {
            foreach (var container in mgr.Containers.Values)
            {
                foreach (var entity in container.ContainedEntities)
                {
                    if (MetaData(entity).EntityPrototype?.ID == targetId)
                        return true;
                }
            }
        }

        return false;
    }

    private bool PlayerHasWeapon(EntityUid player, EntProtoId? protoId)
    {
        if (protoId == null) return false;
        var targetId = protoId.Value.Id;
        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (MetaData(held).EntityPrototype?.ID == targetId)
                return true;
        }
        return false;
    }

    private void ApplyOutline(SpriteComponent sprite, ShopGlowState state)
    {
        var protoId = state switch
        {
            ShopGlowState.Owned => ShaderOwned,
            ShopGlowState.Affordable => ShaderAffordable,
            _ => ShaderUnaffordable,
        };
        sprite.PostShader = _prototypeManager.Index(protoId).InstanceUnique();
    }

    private void ClearAllShaders()
    {
        var query = EntityQueryEnumerator<FSShopWeaponComponent, SpriteComponent>();
        while (query.MoveNext(out _, out _, out var sprite))
        {
            sprite.PostShader = null;
        }
        _lastGlowState.Clear();
    }
}
