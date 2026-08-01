using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.Research;
using Content.Shared._FinalStand.Science;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Shop;

public sealed class FSShopClientSystem : EntitySystem
{
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> LauncherTag = "WeaponGunLauncher";

    private static readonly ProtoId<ShaderPrototype> ShaderAffordable = "FSShopGlowAffordable";
    private static readonly ProtoId<ShaderPrototype> ShaderUnaffordable = "FSShopGlowUnaffordable";
    private static readonly ProtoId<ShaderPrototype> ShaderOwned = "FSShopGlowOwned";
    private static readonly ProtoId<ShaderPrototype> ShaderLocked = "FSShopGlowLocked";
    private const string LockLayerKey = "fs-shop-lock-overlay";

    private enum ShopGlowState { Unaffordable, Affordable, Owned, Locked }

    private HashSet<string> _unlockedResearchNodes = new();
    private bool _isScience;
    private Texture? _lockTexture;

    public int CurrentCredits { get; private set; }
    public Dictionary<string, int> UpgradeLevels { get; private set; } = [];
    public string WeaponTitle { get; private set; } = "";

    public event Action? CreditsChanged;
    public event Action? UpgradeLevelsChanged;
    public event Action? RefreshNeeded;
    public event Action? SellCompleted;
    public event Action<string>? SellFailed;

    private readonly Dictionary<EntityUid, ShopGlowState> _lastGlowState = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnWalletUpdate);
        SubscribeNetworkEvent<UpgradeLevelsUpdatedEvent>(OnUpgradesUpdated);
        SubscribeNetworkEvent<FSShopSellCompletedEvent>(OnSellCompleted);
        SubscribeNetworkEvent<FSShopSellFailedEvent>(OnSellFailed);
        SubscribeNetworkEvent<FSResearchUnlocksChangedEvent>(OnResearchUnlocksChanged);
        SubscribeNetworkEvent<FSPlayerScienceStatusEvent>(OnScienceStatus);
        SubscribeLocalEvent<HandSelectedEvent>(OnHandSelected);
        SubscribeLocalEvent<HandDeselectedEvent>(OnHandDeselected);
        SubscribeLocalEvent<DidEquipHandEvent>(OnDidEquipHand);
        SubscribeLocalEvent<DidUnequipHandEvent>(OnDidUnequipHand);
        _client.PlayerJoinedServer += OnJoined;
        _client.PlayerLeaveServer += OnLeft;

        _lockTexture = _resourceCache.GetResource<TextureResource>("/Textures/_FinalStand/Interface/Shop/lock_icon.png").Texture;
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
            if (shop.RequiresResearch is { } required && !_unlockedResearchNodes.Contains(required.Id))
                state = ShopGlowState.Locked;
            else if (shop.RequiresScience && !_isScience)
                state = ShopGlowState.Locked;
            else if (player != null && PlayerHasWeapon(player.Value, shop.WeaponProtoId))
                state = ShopGlowState.Owned;
            else if (CurrentCredits >= shop.Price)
                state = ShopGlowState.Affordable;
            else
                state = ShopGlowState.Unaffordable;

            if (_lastGlowState.TryGetValue(uid, out var last) && last == state)
                continue;

            _lastGlowState[uid] = state;
            ApplyOutline(sprite, state);
            ApplyLockOverlay(uid, sprite, state == ShopGlowState.Locked);
        }
    }

    private void ApplyLockOverlay(EntityUid uid, SpriteComponent sprite, bool locked)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), LockLayerKey, out var index, false))
        {
            if (!locked)
                return;

            index = sprite.LayerMapReserveBlank(LockLayerKey);
            _sprite.LayerSetTexture((uid, sprite), index, _lockTexture);
            _sprite.LayerSetScale((uid, sprite), index, new System.Numerics.Vector2(0.5f, 0.5f));
        }

        _sprite.LayerSetVisible((uid, sprite), index, locked);
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

    private void OnJoined(object? _, PlayerEventArgs __)
    {
        CurrentCredits = 0;
        UpgradeLevels = [];
        WeaponTitle = "";
        _isScience = false;
        _lastGlowState.Clear();
        CreditsChanged?.Invoke();
    }

    private void OnLeft(object? _, PlayerEventArgs __)
    {
        CurrentCredits = 0;
        UpgradeLevels = [];
        WeaponTitle = "";
        _isScience = false;
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

    public EntityUid? GetActiveHeldItem()
    {
        var player = _player.LocalSession?.AttachedEntity;
        if (player == null || !TryComp<HandsComponent>(player.Value, out var hands))
            return null;
        if (hands.ActiveHandId == null)
            return null;
        _hands.TryGetHeldItem((player.Value, hands), hands.ActiveHandId, out var held);
        return held;
    }

    public bool IsHoldingAnyGun()
    {
        var held = GetActiveHeldItem();
        return held is { } uid && HasComp<GunComponent>(uid);
    }

    // Launchers still HasComp<GunComponent>, so this excludes them explicitly.
    public bool IsHoldingNonLauncherGun()
    {
        var held = GetActiveHeldItem();
        return held is { } uid && HasComp<GunComponent>(uid) && !_tags.HasTag(uid, LauncherTag);
    }

    public bool IsHoldingExplosive()
    {
        var held = GetActiveHeldItem();
        return held is { } uid && (_tags.HasTag(uid, LauncherTag) || HasComp<FSGrenadePackComponent>(uid));
    }

    public bool IsHoldingMelee()
    {
        var held = GetActiveHeldItem();
        return held is { } uid && HasComp<MeleeWeaponComponent>(uid);
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

    private void OnResearchUnlocksChanged(FSResearchUnlocksChangedEvent ev)
    {
        _unlockedResearchNodes = ev.UnlockedNodes;
        _lastGlowState.Clear();
        ResearchNodesChanged?.Invoke();
    }

    public event Action? ResearchNodesChanged;

    public bool IsResearchNodeUnlocked(string nodeId) => _unlockedResearchNodes.Contains(nodeId);

    private void OnScienceStatus(FSPlayerScienceStatusEvent ev)
    {
        _isScience = ev.IsScience;
        _lastGlowState.Clear();
    }

    private void OnSellCompleted(FSShopSellCompletedEvent _) => SellCompleted?.Invoke();

    private void OnSellFailed(FSShopSellFailedEvent ev) => SellFailed?.Invoke(ev.Reason);

    public EntityUid? GetLocalPlayer() => _player.LocalSession?.AttachedEntity;

    private static readonly string[] InventorySlots = ["belt", "suitstorage", "pocket1", "pocket2"];

    public bool PlayerHasWeaponInInventory(EntityUid? player, EntProtoId? protoId, List<EntProtoId>? aliases = null)
    {
        if (player == null || protoId == null) return false;
        var targetId = protoId.Value.Id;

        bool Matches(EntityUid ent)
        {
            var id = MetaData(ent).EntityPrototype?.ID;
            if (id == null)
                return false;
            if (id == targetId)
                return true;
            if (aliases == null)
                return false;
            foreach (var alias in aliases)
            {
                if (id == alias.Id)
                    return true;
            }
            return false;
        }

        foreach (var held in _hands.EnumerateHeld(player.Value))
        {
            if (Matches(held))
                return true;
        }

        foreach (var slot in InventorySlots)
        {
            if (_inventory.TryGetSlotEntity(player.Value, slot, out var item) && item != null
                && Matches(item.Value))
                return true;
        }

        if (_inventory.TryGetSlotEntity(player.Value, "back", out var back) && back != null
            && TryComp<ContainerManagerComponent>(back.Value, out var mgr))
        {
            foreach (var container in mgr.Containers.Values)
            {
                foreach (var entity in container.ContainedEntities)
                {
                    if (Matches(entity))
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
            ShopGlowState.Locked => ShaderLocked,
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
