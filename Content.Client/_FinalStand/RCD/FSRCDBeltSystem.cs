// Build Belt layout: while an engineer holds the RCD, the belt sits above the hands. Z steps to the next slot.
using Content.Client.Hands.Systems;
using Content.Shared._FinalStand.RCD;
using Content.Shared.CCVar;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.RCD;

public sealed partial class FSRCDBeltSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const float BottomMargin = 112f;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan StepDebounce = TimeSpan.FromSeconds(0.2);
    private static readonly TimeSpan ToggleGrace = TimeSpan.FromSeconds(0.6);
    private static readonly SpriteSpecifier StationIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/walls_and_flooring.png"));

    private FSBuildBelt? _belt;
    private Control? _attachedTo;
    private EntityUid? _rcd;
    private bool _stationOpen;
    private TimeSpan _nextRefresh;
    private TimeSpan _nextStep;

    public void Step(EntityUid rcd, FSRCDPalette palette, int direction)
    {
        // The menu opens once predicted and once confirmed per press; only the first counts.
        if (_timing.RealTime < _nextStep || !TryComp<RCDComponent>(rcd, out var comp))
            return;

        _nextStep = _timing.RealTime + StepDebounce;

        var cycle = new List<RCDPrototype>(palette.Pieces);
        if (palette.Deconstruct != null)
            cycle.Add(palette.Deconstruct);
        if (cycle.Count == 0)
            return;

        var index = cycle.FindIndex(p => p.ID == comp.ProtoId.Id);
        Pick(rcd, cycle[((index < 0 ? -1 : index) + direction + cycle.Count) % cycle.Count]);
    }

    // Switching layouts closes the ring, and the server's echo reopens the menu once; don't count that as a Z press.
    public void SuppressSteps()
    {
        _nextStep = _timing.RealTime + ToggleGrace;
    }

    private void Pick(EntityUid rcd, RCDPrototype piece)
    {
        RaiseNetworkEvent(new FSRCDBeltPickEvent(GetNetEntity(rcd), piece.ID));
        _nextRefresh = TimeSpan.Zero;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        Detach();
    }

    public override void FrameUpdate(float frameTime)
    {
        var rcd = HeldRcd();
        if (rcd == null || !_cfg.GetCVar(CCVars.FSRcdBeltLayout))
        {
            Detach();
            return;
        }

        if (_belt == null || _attachedTo != _ui.ActiveScreen || _rcd != rcd)
        {
            Attach();
            _rcd = rcd;
            _stationOpen = false;
            _nextRefresh = TimeSpan.Zero;
        }

        if (_timing.RealTime < _nextRefresh)
            return;

        _nextRefresh = _timing.RealTime + RefreshInterval;
        Refresh(rcd.Value);
    }

    private EntityUid? HeldRcd()
    {
        if (_player.LocalEntity is not { } player
            || !_hands.TryGetActiveItem(player, out var held)
            || !HasComp<FSEngineerOnlyRCDComponent>(held)
            || IsClientSide(held.Value))
            return null;

        return held;
    }

    private void Attach()
    {
        Detach();
        if (_ui.ActiveScreen is not { } screen)
            return;

        _belt = new FSBuildBelt(new FSRCDLook(_cache, ProtoMan, _sprite), EntityManager)
        {
            StationIcon = _sprite.Frame0(StationIcon),
        };
        _belt.OnPick += OnPick;
        _belt.OnToggleLayout += () =>
        {
            _cfg.SetCVar(CCVars.FSRcdBeltLayout, false);
            _cfg.SaveToFile();
        };

        LayoutContainer.SetAnchorPreset(_belt, LayoutContainer.LayoutPreset.CenterBottom);
        LayoutContainer.SetGrowHorizontal(_belt, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetGrowVertical(_belt, LayoutContainer.GrowDirection.Begin);
        LayoutContainer.SetMarginBottom(_belt, -BottomMargin);
        screen.AddChild(_belt);
        _attachedTo = screen;
    }

    private void OnPick(FSRCDEntry entry)
    {
        switch (entry.Kind)
        {
            case FSRCDEntryKind.Station:
                _stationOpen = true;
                break;
            case FSRCDEntryKind.Back:
                _stationOpen = false;
                break;
            default:
                if (_rcd is { } rcd && entry.Piece is { } piece)
                    Pick(rcd, piece);
                break;
        }

        _nextRefresh = TimeSpan.Zero;
    }

    private void Detach()
    {
        _belt?.Orphan();
        _belt = null;
        _attachedTo = null;
        _rcd = null;
    }

    private void Refresh(EntityUid rcd)
    {
        if (_belt == null
            || !TryComp<RCDComponent>(rcd, out var comp)
            || !TryComp<FSEngineerOnlyRCDComponent>(rcd, out var fs))
            return;

        var palette = new FSRCDPalette(ProtoMan, comp, fs);
        var user = _player.LocalEntity;
        var current = comp.ProtoId.Id;

        List<FSRCDEntry> entries;
        if (_stationOpen)
        {
            entries = FSRCDLook.Entries(EntityManager, ProtoMan, _sprite, user, fs, current, palette.Station);
            entries.Add(new FSRCDEntry(FSRCDEntryKind.Back, null, null, -1, false, null, false));
        }
        else
        {
            entries = FSRCDLook.Entries(EntityManager, ProtoMan, _sprite, user, fs, current, palette.Pieces);
            if (palette.Station.Count > 0)
                entries.Add(new FSRCDEntry(FSRCDEntryKind.Station, null, null, -1, false, null,
                    palette.Station.Exists(p => p.ID == current)));
            if (palette.Deconstruct != null)
                entries.AddRange(FSRCDLook.Entries(EntityManager, ProtoMan, _sprite, user, fs, current, [palette.Deconstruct]));
        }

        _belt.Entries = entries;
        _belt.User = user;
        _belt.Rcd = fs;
        _belt.Resize();
    }
}
