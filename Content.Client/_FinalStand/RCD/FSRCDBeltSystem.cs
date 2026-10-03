// Build Belt layout: while an engineer holds the RCD, a slot bar sits above the hands. Each slot shows how many of
// that piece you can build right now; the card above it explains the selected piece. Z steps to the next slot.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client.Hands.Systems;
using Content.Client.Message;
using Content.Shared._FinalStand.RCD;
using Content.Shared.CCVar;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.RCD;

public sealed class FSRCDBeltSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private HandsSystem _hands = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const float SlotSize = 60f;
    private const float BottomMargin = 112f;
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan StepDebounce = TimeSpan.FromSeconds(0.2);

    private BoxContainer? _root;
    private RichTextLabel? _card;
    private BoxContainer? _slots;
    private Control? _attachedTo;
    private EntityUid? _rcd;
    private bool _stationOpen;
    private int _builtFor;
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
        var next = cycle[((index < 0 ? -1 : index) + direction + cycle.Count) % cycle.Count];
        Pick(rcd, next);
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

        if (_root == null || _attachedTo != _ui.ActiveScreen || _rcd != rcd)
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

        _card = new RichTextLabel { MaxWidth = 340 };
        var toggle = new Button { Text = Loc.GetString("fs-rcd-switch-to-ring"), StyleClasses = { "OpenBoth" } };
        toggle.OnPressed += _ =>
        {
            _cfg.SetCVar(CCVars.FSRcdBeltLayout, false);
            _cfg.SaveToFile();
        };

        var cardRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Children =
            {
                new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, Children = { _card, new Label { Text = Loc.GetString("fs-rcd-belt-hint"), FontColorOverride = FSPalette.TextDim } } },
                toggle,
            },
        };

        _slots = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Children =
            {
                Panel(cardRow, FSPalette.PanelBack, new Thickness(8, 6)),
                new CenterContainer { Children = { _slots } },
            },
        };

        LayoutContainer.SetAnchorPreset(_root, LayoutContainer.LayoutPreset.CenterBottom);
        LayoutContainer.SetGrowHorizontal(_root, LayoutContainer.GrowDirection.Both);
        LayoutContainer.SetGrowVertical(_root, LayoutContainer.GrowDirection.Begin);
        LayoutContainer.SetMarginBottom(_root, -BottomMargin);
        screen.AddChild(_root);
        _attachedTo = screen;
        _builtFor = 0;
    }

    private void Detach()
    {
        _root?.Orphan();
        _root = null;
        _card = null;
        _slots = null;
        _attachedTo = null;
        _rcd = null;
    }

    private void Refresh(EntityUid rcd)
    {
        if (_slots == null || _card == null
            || !TryComp<RCDComponent>(rcd, out var comp)
            || !TryComp<FSEngineerOnlyRCDComponent>(rcd, out var fs))
            return;

        var palette = new FSRCDPalette(ProtoMan, comp, fs);
        var user = _player.LocalEntity;
        var pieces = _stationOpen ? palette.Station : palette.Pieces;

        _slots.RemoveAllChildren();
        var number = 1;
        foreach (var piece in pieces)
            _slots.AddChild(Slot(rcd, number++, piece, fs, comp, user));

        if (!_stationOpen && palette.Station.Count > 0)
            _slots.AddChild(StationSlot(number++, open: true));
        else if (_stationOpen)
            _slots.AddChild(StationSlot(number++, open: false));

        if (!_stationOpen && palette.Deconstruct != null)
            _slots.AddChild(Slot(rcd, number, palette.Deconstruct, fs, comp, user));

        _card.SetMarkup(FSRCDPalette.Info(EntityManager, ProtoMan, user, fs, ProtoMan.Index(comp.ProtoId)));
    }

    private Control Slot(EntityUid rcd, int number, RCDPrototype piece, FSEngineerOnlyRCDComponent fs, RCDComponent comp, EntityUid? user)
    {
        var selected = piece.ID == comp.ProtoId.Id;
        var affordable = FSRCDPalette.Affordable(EntityManager, ProtoMan, user, fs, piece);
        var deconstruct = piece.Mode == RcdMode.Deconstruct;

        var button = new Button { SetSize = new Vector2(SlotSize, SlotSize + 6), ToolTip = FSRCDText.Name(ProtoMan, piece) };
        button.OnPressed += _ => Pick(rcd, piece);

        var layout = new LayoutContainer { SetSize = new Vector2(SlotSize, SlotSize + 6) };
        if (piece.Sprite is { } sprite)
        {
            var icon = new TextureRect
            {
                Texture = _sprite.Frame0(sprite),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                SetSize = new Vector2(36, 36),
            };
            LayoutContainer.SetPosition(icon, new Vector2((SlotSize - 36) / 2f, 13));
            layout.AddChild(icon);
        }

        var numberLabel = new Label { Text = number.ToString(), FontColorOverride = FSPalette.TextDim };
        LayoutContainer.SetPosition(numberLabel, new Vector2(3, -2));
        layout.AddChild(numberLabel);

        if (affordable >= 0)
        {
            var countLabel = new Label
            {
                Text = $"×{affordable}",
                FontColorOverride = affordable > 0 ? FSPalette.Ok : FSPalette.DangerSoft,
            };
            LayoutContainer.SetAnchorAndMarginPreset(countLabel, LayoutContainer.LayoutPreset.TopRight);
            LayoutContainer.SetGrowHorizontal(countLabel, LayoutContainer.GrowDirection.Begin);
            LayoutContainer.SetMarginTop(countLabel, -2);
            LayoutContainer.SetMarginRight(countLabel, -3);
            layout.AddChild(countLabel);
        }

        var bar = new PanelContainer
        {
            SetSize = new Vector2(SlotSize - 8, 3),
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = deconstruct ? FSPalette.Danger : affordable != 0 ? FSPalette.Ok : FSPalette.Danger,
            },
        };
        LayoutContainer.SetPosition(bar, new Vector2(4, SlotSize + 1));
        layout.AddChild(bar);
        button.AddChild(layout);

        return Panel(button, selected ? FSPalette.ButtonPressBack : FSPalette.CellBack, new Thickness(1),
            selected ? FSPalette.Money : FSPalette.CellEdge);
    }

    private Control StationSlot(int number, bool open)
    {
        var button = new Button
        {
            SetSize = new Vector2(SlotSize, SlotSize + 6),
            Text = Loc.GetString(open ? "fs-rcd-belt-station" : "fs-rcd-belt-back"),
            ToolTip = Loc.GetString("fs-rcd-ring-station"),
        };
        button.OnPressed += _ =>
        {
            _stationOpen = open;
            _nextRefresh = TimeSpan.Zero;
        };
        return Panel(button, FSPalette.CellBack, new Thickness(1), FSPalette.CellEdge);
    }

    private static PanelContainer Panel(Control child, Color back, Thickness padding, Color? edge = null)
    {
        return new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = back,
                BorderColor = edge ?? FSPalette.PanelEdge,
                BorderThickness = new Thickness(1),
                ContentMarginLeftOverride = padding.Left,
                ContentMarginRightOverride = padding.Right,
                ContentMarginTopOverride = padding.Top,
                ContentMarginBottomOverride = padding.Bottom,
            },
            Children = { child },
        };
    }
}
