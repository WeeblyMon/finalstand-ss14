using Content.Client._FinalStand.Perks.UI;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Leveling;
using Robust.Client.Player;

namespace Content.Client._FinalStand.Perks;

public sealed partial class FSPerkShopSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private FSPerkShopAccessSystem _shopAccess = default!;

    private PerkShopWindow? _window;
    private FSPerksStateEvent? _cachedState;
    private int _cachedLevel = 1;
    private int _cachedPrestige;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSPerksStateEvent>(OnPerksState);
        SubscribeNetworkEvent<FSLevelingUpdatedEvent>(OnLevelingUpdated);
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnWalletUpdated);
        SubscribeNetworkEvent<FSOpenPerkShopEvent>(_ => OpenWindow());
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_window is not { Disposed: false, IsOpen: true })
            return;

        if (_player.LocalEntity is { } local && !_shopAccess.CanUse(local))
            _window.Close();
    }

    public void OpenWindow()
    {
        if (_window == null || _window.Disposed)
        {
            _window = new PerkShopWindow();
            _window.OnBuyPerk    += msg => RaiseNetworkEvent(msg);
            _window.OnEquipPerk  += msg => RaiseNetworkEvent(msg);
            _window.OnUnequipPerk += msg => RaiseNetworkEvent(msg);
            _window.OnSaveLoadout   += msg => RaiseNetworkEvent(msg);
            _window.OnLoadLoadout   += msg => RaiseNetworkEvent(msg);
            _window.OnRespecRequested += () =>
                RaiseNetworkEvent(new FSRespecPerkMessage());
            _window.OnPrestigeRequested += () =>
                RaiseNetworkEvent(new FSPrestigeRequestMessage());

            if (_cachedState != null)
                _window.UpdateState(_cachedState);

            _window.UpdateLeveling(_cachedLevel, _cachedPrestige);
        }

        _window.OpenCentered();
        RaiseNetworkEvent(new FSPerkStateRequestMessage());
    }

    private void OnPerksState(FSPerksStateEvent ev)
    {
        _cachedState = ev;
        if (_window is { Disposed: false, IsOpen: true })
            _window.UpdateState(ev);
    }

    private void OnLevelingUpdated(FSLevelingUpdatedEvent ev)
    {
        _cachedLevel   = ev.Level;
        _cachedPrestige = ev.PrestigeLevel;
        if (_window is { Disposed: false, IsOpen: true })
            _window.UpdateLeveling(ev.Level, ev.PrestigeLevel);
    }

    private void OnWalletUpdated(WalletUpdatedEvent ev)
    {
        if (_cachedState == null) return;
        _cachedState.PerkPoints = ev.PerkPoints;
        if (_window is { Disposed: false, IsOpen: true })
            _window.UpdateState(_cachedState);
    }

    public int GetSlottedPerkLevel(string id)
    {
        if (_cachedState == null) return 0;
        return _cachedState.Slots.Contains(id) && _cachedState.Levels.TryGetValue(id, out var lvl) ? lvl : 0;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Dispose();
        _window = null;
    }
}
