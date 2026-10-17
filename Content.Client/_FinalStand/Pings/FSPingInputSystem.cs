using System.Numerics;
using Content.Client.Gameplay;
using Content.Shared._FinalStand.Pings;
using Content.Shared.Input;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Pings;

// A single tap waits out the double-tap window before it is sent.
public sealed partial class FSPingInputSystem : EntitySystem
{
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly TimeSpan HoldTime = TimeSpan.FromSeconds(0.15);
    private static readonly TimeSpan DoubleTap = TimeSpan.FromSeconds(0.3);
    private static readonly TimeSpan Chatter = TimeSpan.FromSeconds(0.08);
    private static readonly TimeSpan AfterDanger = TimeSpan.FromSeconds(0.4);
    private const float DragDistance = 20f;
    private const float DoubleTapDistance = 30f;

    private FSPingLook? _look;
    private FSPingWheel? _wheel;
    private bool _holding;
    private TimeSpan _pressedAt;
    private Vector2 _pressScreen;
    private EntityCoordinates _pressCoords;
    private EntityUid? _pressTarget;
    private TimeSpan _lastTap;
    private Vector2 _lastTapScreen;
    private (EntityCoordinates Coords, EntityUid? Target, TimeSpan SendAt)? _pendingTap;
    private TimeSpan _quietUntil;

    public override void Initialize()
    {
        base.Initialize();
        CommandBinds.Builder
            .Bind(ContentKeyFunctions.FSPing, InputCmdHandler.FromDelegate(OnPingDown, OnPingUp, false))
            .Bind(ContentKeyFunctions.FSPingWheel, InputCmdHandler.FromDelegate(OnWheelKey, handle: false))
            .Register<FSPingInputSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        CommandBinds.Unregister<FSPingInputSystem>();
        CloseWheel();
    }

    private void OnPingDown(ICommonSession? session)
    {
        if (_wheel != null || _timing.RealTime - _lastTap < Chatter || _timing.RealTime < _quietUntil || !Capture())
            return;

        _holding = true;
        _pressedAt = _timing.RealTime;
    }

    private void OnPingUp(ICommonSession? session)
    {
        if (!_holding)
            return;

        _holding = false;
        if (_wheel != null)
        {
            var picked = _wheel.Selected;
            CloseWheel();
            if (picked >= 0)
                Send(FSPingWheel.Slices[picked].Type);
            return;
        }

        var now = _timing.RealTime;
        var doubled = _pendingTap != null && now - _lastTap < DoubleTap && (_pressScreen - _lastTapScreen).Length() < DoubleTapDistance;
        _lastTap = now;
        _lastTapScreen = _pressScreen;
        if (doubled)
        {
            _pendingTap = null;
            _quietUntil = now + AfterDanger;
            Send(FSPingType.Danger);
            return;
        }

        _pendingTap = (_pressCoords, _pressTarget, now + DoubleTap);
    }

    private void OnWheelKey(ICommonSession? session)
    {
        if (_wheel != null)
        {
            CloseWheel();
            return;
        }

        if (Capture())
            OpenWheel(true);
    }

    private bool Capture()
    {
        if (_state.CurrentState is not GameplayStateBase screen || _ui.CurrentlyHovered is not IViewportControl)
            return false;

        _pressScreen = _input.MouseScreenPosition.Position;
        var map = _eye.PixelToMap(_input.MouseScreenPosition);
        if (map.MapId == MapId.Nullspace)
            return false;

        _pressCoords = _transform.ToCoordinates(map);
        _pressTarget = screen.GetClickedEntity(map);
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        if (_pendingTap is { } tap && _timing.RealTime >= tap.SendAt)
        {
            _pendingTap = null;
            RaiseNetworkEvent(new FSPingRequestEvent(GetNetCoordinates(tap.Coords), tap.Target is { } target ? GetNetEntity(target) : null, null));
        }

        if (_wheel != null)
        {
            _wheel.Track(_input.MouseScreenPosition.Position);
            return;
        }

        if (!_holding)
            return;

        var moved = (_input.MouseScreenPosition.Position - _pressScreen).Length() >= DragDistance;
        if (moved || _timing.RealTime - _pressedAt >= HoldTime)
            OpenWheel(false);
    }

    private void OpenWheel(bool clickToPick)
    {
        _look ??= new FSPingLook(_cache);
        _wheel = new FSPingWheel(_look, _pressScreen, clickToPick);
        _wheel.OnPicked += type =>
        {
            CloseWheel();
            Send(type);
        };
        _wheel.OnCancelled += CloseWheel;
        _ui.PopupRoot.AddChild(_wheel);
    }

    private void CloseWheel()
    {
        if (_wheel == null)
            return;

        _wheel.Orphan();
        _wheel = null;
    }

    private void Send(FSPingType? forced)
    {
        RaiseNetworkEvent(new FSPingRequestEvent(GetNetCoordinates(_pressCoords),
            forced == null && _pressTarget is { } target ? GetNetEntity(target) : null, forced));
    }
}
