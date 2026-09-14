using Content.Client._FinalStand.Grenades;
using Content.Client.Hands.Systems;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared.Input;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.SmartReload;

public sealed partial class FSSmartReloadInputSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private static readonly TimeSpan HoldThreshold = TimeSpan.FromMilliseconds(400);

    [Dependency] private FSAmmoWheel _ammoWheel = default!;
    [Dependency] private FSThrowableWheel _throwWheel = default!;

    private TimeSpan _pressTime;
    private bool _isHolding;
    private bool _wheelOpened;

    private TimeSpan _grenadePressTime;
    private bool _grenadeHeld;
    private bool _throwWheelOpened;

    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ReloadWeapon,
                InputCmdHandler.FromDelegate(OnReloadDown, OnReloadUp))
            .Bind(ContentKeyFunctions.QuickGrenade,
                InputCmdHandler.FromDelegate(OnGrenadeDown, OnGrenadeUp))
            .Register<FSSmartReloadInputSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<FSSmartReloadInputSystem>();
    }

    // Past the threshold the hold stops being a reload and becomes a choice of magazine.
    // This used to eject instead; ejecting is gone.
    public override void Update(float frameTime)
    {
        UpdateGrenadeHold();

        if (!_isHolding || _wheelOpened)
            return;

        if (_gameTiming.CurTime - _pressTime < HoldThreshold)
            return;

        _wheelOpened = true;

        if (ResolveActiveGun() is not { } gun)
            return;

        // Nothing to choose from: fall through so the release still performs a normal reload.
        if (!_ammoWheel.TryOpen(gun))
            _wheelOpened = false;
    }

    private void UpdateGrenadeHold()
    {
        if (!_grenadeHeld || _throwWheelOpened)
            return;

        if (_gameTiming.CurTime - _grenadePressTime < HoldThreshold)
            return;

        _throwWheelOpened = true;

        // Only one pack type carried: nothing to pick, so the release just throws it.
        if (!_throwWheel.TryOpen())
            _throwWheelOpened = false;
    }

    // Tap throws, hold picks. The throw moved from press to release so the hold can mean
    // something; a tap still costs only the release, which is imperceptible.
    private void OnGrenadeDown(ICommonSession? session)
    {
        _grenadePressTime = _gameTiming.CurTime;
        _grenadeHeld = true;
        _throwWheelOpened = false;
    }

    private void OnGrenadeUp(ICommonSession? session)
    {
        _grenadeHeld = false;

        if (_throwWheelOpened)
            return;

        var screenPos = _inputManager.MouseScreenPosition;
        var mapCoords = _eyeManager.PixelToMap(screenPos);
        RaiseNetworkEvent(new FSQuickGrenadeMessage { CursorWorldPos = mapCoords.Position });
    }

    private void OnReloadDown(ICommonSession? session)
    {
        _pressTime = _gameTiming.CurTime;
        _isHolding = true;
        _wheelOpened = false;
    }

    private void OnReloadUp(ICommonSession? session)
    {
        _isHolding = false;

        // The wheel owns the release: the player picks from it, or dismisses it.
        if (_wheelOpened)
            return;

        if (ResolveActiveGun() is not { } gun)
            return;

        RaiseNetworkEvent(new FSSmartReloadMessage { Gun = GetNetEntity(gun) });
    }

    // Returns the gun the player intends to reload/eject from the active hand.
    // A virtual item is resolved back to the gun it mirrors, which must still be held.
    private EntityUid? ResolveActiveGun()
    {
        var active = _hands.GetActiveHandEntity();
        if (active == null)
            return null;

        if (HasComp<GunComponent>(active.Value))
            return active.Value;

        if (_playerManager.LocalSession?.AttachedEntity is not { } user)
            return null;

        if (!TryComp<VirtualItemComponent>(active.Value, out var virt))
            return null;

        if (!HasComp<GunComponent>(virt.BlockingEntity))
            return null;

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (held == virt.BlockingEntity)
                return virt.BlockingEntity;
        }

        return null;
    }
}
