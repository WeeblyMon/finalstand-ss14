using Content.Client._FinalStand.Perks.UI;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Leveling;

namespace Content.Client._FinalStand.Perks;

public sealed class FSPerkShopSystem : EntitySystem
{
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

    public void OpenWindow()
    {
        if (_window == null || _window.Disposed)
        {
            _window = new PerkShopWindow();
            _window.OnBuyPerk    += msg => RaiseNetworkEvent(msg);
            _window.OnEquipPerk  += msg => RaiseNetworkEvent(msg);
            _window.OnUnequipAugment += msg => RaiseNetworkEvent(msg);
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

    public int GetSlottedAugmentLevel(string id)
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
