using Content.Client.Hands.Systems;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared.Input;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.SmartReload;

public sealed class FSSmartReloadInputSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;

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

        var gun = _hands.GetActiveHandEntity();
        if (gun == null || !HasComp<GunComponent>(gun.Value))
            return;

        RaiseNetworkEvent(new FSEjectMessage { Gun = GetNetEntity(gun.Value) });
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

        var gun = _hands.GetActiveHandEntity();
        if (gun == null || !HasComp<GunComponent>(gun.Value))
            return;

        RaiseNetworkEvent(new FSSmartReloadMessage { Gun = GetNetEntity(gun.Value) });
    }
}
