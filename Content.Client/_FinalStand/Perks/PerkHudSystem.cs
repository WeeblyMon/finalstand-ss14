using Content.Shared._FinalStand.Perks;
using Robust.Client;
using Robust.Client.Graphics;
using Robust.Client.Player;

namespace Content.Client._FinalStand.Perks;

public sealed class PerkHudSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IBaseClient _client = default!;

    private PerkHudOverlay? _overlay;
    private EntityUid? _lastEntity;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PerkAddedEvent>(OnPerkAdded);
        SubscribeNetworkEvent<PerkRemovedAllEvent>(OnPerkRemovedAll);
        _client.PlayerJoinedServer += OnJoined;
        _client.PlayerLeaveServer  += OnLeft;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _client.PlayerJoinedServer -= OnJoined;
        _client.PlayerLeaveServer  -= OnLeft;
        RemoveOverlay();
    }

    public override void FrameUpdate(float frameTime)
    {
        var current = _player.LocalSession?.AttachedEntity;
        if (current == _lastEntity)
            return;

        _lastEntity = current;
        SyncFromComponent();
    }

    private void OnJoined(object? _, PlayerEventArgs __) => SyncFromComponent();

    private void OnLeft(object? _, PlayerEventArgs __)
    {
        _lastEntity = null;
        RemoveOverlay();
    }

    // Events are now SinglePlayer-targeted so no entity-ID check needed.
    private void OnPerkAdded(PerkAddedEvent ev) => SyncFromComponent();

    private void OnPerkRemovedAll(PerkRemovedAllEvent ev) => RemoveOverlay();

    private void SyncFromComponent()
    {
        var localEntity = _player.LocalSession?.AttachedEntity;
        if (localEntity == null)
        {
            RemoveOverlay();
            return;
        }

        if (!TryComp<PerkComponent>(localEntity.Value, out var perks) || perks.ActivePerks.Count == 0)
        {
            RemoveOverlay();
            return;
        }

        _overlay ??= new PerkHudOverlay();
        _overlay.ActivePerks = new List<PerkType>(perks.ActivePerks);

        if (!_overlayManager.HasOverlay<PerkHudOverlay>())
            _overlayManager.AddOverlay(_overlay);
    }

    private void RemoveOverlay()
    {
        if (_overlay == null)
            return;

        _overlayManager.RemoveOverlay(_overlay);
        _overlay = null;
    }
}
