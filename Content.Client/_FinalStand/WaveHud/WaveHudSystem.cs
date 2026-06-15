using Content.Shared._FinalStand.Augments;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.WaveHud;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.WaveHud;

public sealed class WaveHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IBaseClient _client = default!;

    private WaveHudOverlay? _overlay;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WaveCounterUpdateEvent>(OnWaveUpdate);
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnWalletUpdate);
        SubscribeNetworkEvent<FSEnemyCountEvent>(OnEnemyCount);
        SubscribeNetworkEvent<FSAugmentsStateEvent>(OnAugmentsState);
        SubscribeLocalEvent<LocalPlayerAttachedEvent>(OnLocalPlayerAttached);
        _client.PlayerJoinedServer += OnPlayerJoinedServer;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _client.PlayerJoinedServer -= OnPlayerJoinedServer;
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }

    private void OnPlayerJoinedServer(object? sender, PlayerEventArgs _)
    {
        RaiseNetworkEvent(new WalletRequestEvent());
        RaiseNetworkEvent(new FSAugmentStateRequestMessage());
    }

    private void OnLocalPlayerAttached(LocalPlayerAttachedEvent _)
    {
        RaiseNetworkEvent(new FSAugmentStateRequestMessage());
    }

    private WaveHudOverlay? EnsureOverlay()
    {
        if (_overlay != null)
            return _overlay;
        try
        {
            var textures = new Texture[10];
            for (var i = 0; i < 10; i++)
                textures[i] = _resourceCache
                    .GetResource<TextureResource>(new ResPath($"/Textures/_FinalStand/WaveCounter/{i}.png"))
                    .Texture;
            _overlay = new WaveHudOverlay(textures);
            _overlayManager.AddOverlay(_overlay);
            return _overlay;
        }
        catch (Exception e)
        {
            Log.Error($"[WaveHud] Failed to load digit textures: {e.Message}");
            return null;
        }
    }

    private void OnWaveUpdate(WaveCounterUpdateEvent ev)
    {
        if (EnsureOverlay() is { } overlay)
            overlay.CurrentWave = ev.Wave;
    }

    private void OnWalletUpdate(WalletUpdatedEvent ev)
    {
        if (EnsureOverlay() is { } overlay)
            overlay.CurrentCredits = ev.Credits;
    }

    private void OnEnemyCount(FSEnemyCountEvent ev)
    {
        if (EnsureOverlay() is { } overlay)
        {
            overlay.EnemiesAlive = ev.Alive;
            overlay.EnemiesTotal = ev.Total;
        }
    }

    private void OnAugmentsState(FSAugmentsStateEvent ev)
    {
        if (EnsureOverlay() is not { } overlay) return;
        overlay.ActiveSlots   = ev.Slots;
        overlay.AugmentLevels = ev.Levels;
    }
}
