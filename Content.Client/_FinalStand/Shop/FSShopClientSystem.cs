using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
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
    /// <summary>Raised when the local player's active held item changes — open shop BUI should send a refresh.</summary>
    public event Action? RefreshNeeded;

    private readonly Dictionary<EntityUid, bool> _lastAffordability = [];
    private EntityUid? _lastActiveItem;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnWalletUpdate);
        SubscribeNetworkEvent<UpgradeLevelsUpdatedEvent>(OnUpgradesUpdated);
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

        // Detect active hand item changes to trigger a shop-level refresh.
        var localEntity = _player.LocalSession?.AttachedEntity;
        EntityUid? currentItem = null;
        if (localEntity != null
            && TryComp<HandsComponent>(localEntity.Value, out var hands)
            && hands.ActiveHandId != null)
        {
            _hands.TryGetHeldItem((localEntity.Value, hands), hands.ActiveHandId, out currentItem);
        }

        if (currentItem != _lastActiveItem)
        {
            _lastActiveItem = currentItem;
            RefreshNeeded?.Invoke();
        }
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

    private void OnJoined(object? _, PlayerEventArgs __)
    {
        CurrentCredits = 0;
        UpgradeLevels = [];
        WeaponTitle = "";
        _lastActiveItem = null;
        _lastAffordability.Clear();
        CreditsChanged?.Invoke();
    }

    private void OnLeft(object? _, PlayerEventArgs __)
    {
        CurrentCredits = 0;
        UpgradeLevels = [];
        WeaponTitle = "";
        _lastActiveItem = null;
        ClearAllShaders();
        CreditsChanged?.Invoke();
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
