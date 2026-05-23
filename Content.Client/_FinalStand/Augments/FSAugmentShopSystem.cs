using Content.Client._FinalStand.Augments.UI;
using Content.Shared._FinalStand.Augments;
using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.Leveling;

namespace Content.Client._FinalStand.Augments;

public sealed class FSAugmentShopSystem : EntitySystem
{
    private AugmentShopWindow? _window;
    private FSAugmentsStateEvent? _cachedState;
    private int _cachedLevel = 1;
    private int _cachedPrestige;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSAugmentsStateEvent>(OnAugmentsState);
        SubscribeNetworkEvent<FSLevelingUpdatedEvent>(OnLevelingUpdated);
        SubscribeNetworkEvent<WalletUpdatedEvent>(OnWalletUpdated);
    }

    public void OpenWindow()
    {
        if (_window == null || _window.Disposed)
        {
            _window = new AugmentShopWindow();
            _window.OnBuyAugment    += msg => RaiseNetworkEvent(msg);
            _window.OnEquipAugment  += msg => RaiseNetworkEvent(msg);
            _window.OnUnequipAugment += msg => RaiseNetworkEvent(msg);
            _window.OnSaveLoadout   += msg => RaiseNetworkEvent(msg);
            _window.OnLoadLoadout   += msg => RaiseNetworkEvent(msg);
            _window.OnPrestigeRequested += () =>
                RaiseNetworkEvent(new FSPrestigeRequestMessage());

            if (_cachedState != null)
                _window.UpdateState(_cachedState);

            _window.UpdateLeveling(_cachedLevel, _cachedPrestige);
        }

        _window.OpenCentered();
        RaiseNetworkEvent(new FSAugmentStateRequestMessage());
    }

    private void OnAugmentsState(FSAugmentsStateEvent ev)
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
        _cachedState.AugmentPoints = ev.AugmentPoints;
        if (_window is { Disposed: false, IsOpen: true })
            _window.UpdateState(_cachedState);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Dispose();
        _window = null;
    }
}
