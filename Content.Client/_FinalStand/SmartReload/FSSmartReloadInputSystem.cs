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

    private TimeSpan _pressTime;
    private bool _isHolding;
    private bool _ejected;

    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.ReloadWeapon,
                InputCmdHandler.FromDelegate(OnReloadDown, OnReloadUp))
            .Bind(ContentKeyFunctions.QuickGrenade,
                InputCmdHandler.FromDelegate(OnGrenadeDown, null))
            .Register<FSSmartReloadInputSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<FSSmartReloadInputSystem>();
    }

    public override void Update(float frameTime)
    {
        if (!_isHolding || _ejected)
            return;

        if (_gameTiming.CurTime - _pressTime < HoldThreshold)
            return;

        _ejected = true;

        if (ResolveActiveGun() is not { } gun)
            return;

        RaiseNetworkEvent(new FSEjectMessage { Gun = GetNetEntity(gun) });
    }

    private void OnGrenadeDown(ICommonSession? session)
    {
        var screenPos = _inputManager.MouseScreenPosition;
        var mapCoords = _eyeManager.PixelToMap(screenPos);
        RaiseNetworkEvent(new FSQuickGrenadeMessage { CursorWorldPos = mapCoords.Position });
    }

    private void OnReloadDown(ICommonSession? session)
    {
        _pressTime = _gameTiming.CurTime;
        _isHolding = true;
        _ejected = false;
    }

    private void OnReloadUp(ICommonSession? session)
    {
        _isHolding = false;

        if (_ejected)
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
