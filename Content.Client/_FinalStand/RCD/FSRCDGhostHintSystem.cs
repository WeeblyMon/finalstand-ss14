// Asks the server why the tile under the RCD ghost would be refused (or what deconstructing it refunds) and shows the answer.
using Content.Client.Gameplay;
using Content.Client.Hands.Systems;
using Content.Shared._FinalStand.RCD;
using Content.Shared.CCVar;
using Content.Shared.Popups;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Placement;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.RCD;

public sealed partial class FSRCDGhostHintSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPlacementManager _placement = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly TimeSpan ProbeInterval = TimeSpan.FromSeconds(0.4);

    public EntityCoordinates? Tile { get; private set; }
    public string? Text { get; private set; }
    public bool Blocked { get; private set; }
    public bool Deconstructing { get; private set; }
    public bool Rotatable { get; private set; }
    public EntityUid? Rcd { get; private set; }
    public RCDPrototype? Piece { get; private set; }
    public TimeSpan DeniedAt { get; private set; }
    public EntityCoordinates? DeniedTile { get; private set; }
    public Direction Direction => _placement.Direction;

    private static readonly SoundSpecifier DeniedSound = new SoundPathSpecifier("/Audio/_FinalStand/Engineering/denied.ogg",
        AudioParams.Default.WithVolume(-6f));

    private (EntityUid Rcd, EntityCoordinates Tile, string Proto, EntityUid? Target, Direction Dir) _lastKey;
    private TimeSpan _nextProbe;
    private NetCoordinates? _resultTile;
    private string? _resultText;
    private bool _resultBlocked;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSRCDProbeResultEvent>(OnResult);
        SubscribeNetworkEvent<FSRCDDeniedEvent>(OnDenied);
        _overlays.AddOverlay(new FSRCDGhostHintOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSRCDGhostHintOverlay>();
    }

    private void OnResult(FSRCDProbeResultEvent ev)
    {
        _resultTile = ev.Location;
        _resultText = ev.Text;
        _resultBlocked = ev.Blocked;
    }

    private void OnDenied(FSRCDDeniedEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.FSEngDeniedFeedback))
            return;

        DeniedAt = _timing.RealTime;
        DeniedTile = GetCoordinates(ev.Location);
        _audio.PlayGlobal(DeniedSound, Filter.Local(), false);
    }

    public override void FrameUpdate(float frameTime)
    {
        Tile = null;
        Rcd = null;
        Piece = null;
        Text = null;
        Blocked = false;
        Deconstructing = false;
        Rotatable = false;

        if (_player.LocalEntity is not { } player
            || !_hands.TryGetActiveItem(player, out var held)
            || !HasComp<FSEngineerOnlyRCDComponent>(held)
            || !TryComp<RCDComponent>(held, out var rcd)
            || IsClientSide(held.Value))
            return;

        if (!_cfg.GetCVar(CCVars.FSRcdHintSeen))
        {
            _cfg.SetCVar(CCVars.FSRcdHintSeen, true);
            _cfg.SaveToFile();
            _popup.PopupEntity(Loc.GetString("fs-rcd-first-hint"), player, player, PopupType.Large);
        }

        if (_ui.CurrentlyHovered is not IViewportControl)
            return;

        var mapCoords = _eye.PixelToMap(_input.MouseScreenPosition);
        if (!_map.TryFindGridAt(mapCoords, out var gridUid, out var grid))
            return;

        var center = _map.GridTileToLocal(gridUid, grid, _map.TileIndicesFor(gridUid, grid, mapCoords));
        Tile = center;

        if (!_transform.InRange(Transform(player).Coordinates, center, Comp<FSEngineerOnlyRCDComponent>(held.Value).Reach))
        {
            Rcd = held;
            Piece = ProtoMan.Index(rcd.ProtoId);
            Text = Loc.GetString("fs-rcd-too-far");
            Blocked = true;
            return;
        }

        var proto = ProtoMan.Index(rcd.ProtoId);
        Rcd = held;
        Piece = proto;
        Deconstructing = proto.Mode == RcdMode.Deconstruct;
        Rotatable = proto.Mode == RcdMode.ConstructObject && proto.Rotation == RcdRotation.User;
        EntityUid? target = proto.Mode == RcdMode.Deconstruct && _state.CurrentState is GameplayStateBase screen
            ? screen.GetClickedEntity(mapCoords)
            : null;
        if (target is { } clicked && IsClientSide(clicked))
            target = null;

        var key = (held.Value, center, rcd.ProtoId.Id, target, _placement.Direction);
        if (key != _lastKey)
        {
            _lastKey = key;
            _resultTile = null;
            _nextProbe = TimeSpan.Zero;
        }

        if (_timing.RealTime >= _nextProbe)
        {
            _nextProbe = _timing.RealTime + ProbeInterval;
            RaiseNetworkEvent(new FSRCDProbeRequestEvent(GetNetEntity(held.Value), GetNetCoordinates(center), GetNetEntity(target)));
        }

        if (_resultTile is { } resultTile && GetCoordinates(resultTile) == center)
        {
            Text = _resultText;
            Blocked = _resultBlocked;
        }
    }
}
