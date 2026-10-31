using Content.Shared._FinalStand.Research;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Weapons;

// Shows FSHarvesterRpHudOverlay while holding any research weapon; polls every frame instead of hand-change events so it also catches one already held on reconnect/spawn.
public sealed partial class FSHarvesterRpHudSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    private FSHarvesterRpHudOverlay? _overlay;
    private int _points;
    private string? _personalLine;
    private string? _sharedLine;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSStationRpChangedEvent>(OnRpChanged);
        SubscribeNetworkEvent<FSPersonalResearchStateEvent>(OnPersonalResearchState);
        SubscribeNetworkEvent<FSSharedResearchStateEvent>(OnSharedResearchState);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
        {
            _overlayManager.RemoveOverlay(_overlay);
            _overlay = null;
        }
    }

    private void OnRpChanged(FSStationRpChangedEvent ev)
    {
        _points = ev.Points;
        if (_overlay != null)
            _overlay.Points = _points;
    }

    private void OnPersonalResearchState(FSPersonalResearchStateEvent ev)
    {
        _personalLine = ev.NodeId is { } nodeId && _prototype.TryIndex(nodeId, out var node)
            ? Loc.GetString("fs-harvester-hud-personal", ("name", node.Name), ("progress", ev.Progress), ("cost", node.Cost))
            : null;

        UpdateOverlayLine();
    }

    // Fallback for anyone with no personal pick - shows what their default hits are funding.
    private void OnSharedResearchState(FSSharedResearchStateEvent ev)
    {
        _sharedLine = ev.NodeId is { } nodeId && _prototype.TryIndex(nodeId, out var node)
            ? Loc.GetString("fs-harvester-hud-shared", ("name", node.Name), ("progress", ev.Progress), ("cost", node.Cost))
            : null;

        UpdateOverlayLine();
    }

    private void UpdateOverlayLine()
    {
        if (_overlay != null)
            _overlay.PersonalLine = _personalLine ?? _sharedLine;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var player = _player.LocalSession?.AttachedEntity;
        var holding = player != null && IsHoldingResearchWeapon(player.Value);

        if (holding)
        {
            _overlay ??= new FSHarvesterRpHudOverlay();
            _overlay.Points = _points;
            _overlay.PersonalLine = _personalLine ?? _sharedLine;
            if (!_overlayManager.HasOverlay<FSHarvesterRpHudOverlay>())
                _overlayManager.AddOverlay(_overlay);
        }
        else if (_overlay != null && _overlayManager.HasOverlay<FSHarvesterRpHudOverlay>())
        {
            _overlayManager.RemoveOverlay(_overlay);
        }
    }

    private bool IsHoldingResearchWeapon(EntityUid user)
    {
        if (!HasComp<HandsComponent>(user))
            return false;

        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (HasComp<FSResearchHudComponent>(held))
                return true;
        }

        return false;
    }
}
