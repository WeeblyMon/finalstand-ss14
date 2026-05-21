using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Shop;

public sealed class FSShopClientSystem : EntitySystem
{
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private static readonly ProtoId<ShaderPrototype> ShaderAffordable = "FSShopGlowAffordable";
    private static readonly ProtoId<ShaderPrototype> ShaderUnaffordable = "FSShopGlowUnaffordable";

    public int CurrentCredits { get; private set; }
    public Dictionary<string, int> UpgradeLevels { get; private set; } = [];
    public string WeaponTitle { get; private set; } = "";

    public event Action? CreditsChanged;
    public event Action? UpgradeLevelsChanged;
    public event Action? RefreshNeeded;
    public event Action? PerkStateChanged;

    private readonly Dictionary<EntityUid, bool> _lastAffordability = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnWalletUpdate);
        SubscribeNetworkEvent<UpgradeLevelsUpdatedEvent>(OnUpgradesUpdated);
        SubscribeNetworkEvent<PerkAddedEvent>(OnPerkAdded);
        SubscribeNetworkEvent<PerkRemovedAllEvent>(OnPerkRemovedAll);
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
        var query = EntityQueryEnumerator<FSShopWeaponComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var shop, out var sprite))
        {
            var canAfford = CurrentCredits >= shop.Price;
            if (_lastAffordability.TryGetValue(uid, out var last) && last == canAfford)
                continue;

            _lastAffordability[uid] = canAfford;
            ApplyOutline(sprite, canAfford);
        }
    }

    private void OnHandSelected(HandSelectedEvent ev)
    {
        if (_player.LocalSession?.AttachedEntity != ev.User) return;
        RefreshNeeded?.Invoke();
    }

    private void OnHandDeselected(HandDeselectedEvent ev)
    {
        if (_player.LocalSession?.AttachedEntity != ev.User) return;
        RefreshNeeded?.Invoke();
    }

    private void OnDidEquipHand(DidEquipHandEvent ev)
    {
        if (_player.LocalSession?.AttachedEntity != ev.User) return;
        RefreshNeeded?.Invoke();
    }

    private void OnDidUnequipHand(DidUnequipHandEvent ev)
    {
        if (_player.LocalSession?.AttachedEntity != ev.User) return;
        RefreshNeeded?.Invoke();
    }

    private void OnWalletUpdate(WalletUpdatedEvent ev)
    {
        CurrentCredits = ev.Credits;
        _lastAffordability.Clear();
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
        _lastAffordability.Clear();
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

    private void ApplyOutline(SpriteComponent sprite, bool canAfford)
    {
        var protoId = canAfford ? ShaderAffordable : ShaderUnaffordable;
        sprite.PostShader = _prototypeManager.Index(protoId).InstanceUnique();
    }

    private void ClearAllShaders()
    {
        var query = EntityQueryEnumerator<FSShopWeaponComponent, SpriteComponent>();
        while (query.MoveNext(out _, out _, out var sprite))
        {
            sprite.PostShader = null;
        }
        _lastAffordability.Clear();
    }
}
