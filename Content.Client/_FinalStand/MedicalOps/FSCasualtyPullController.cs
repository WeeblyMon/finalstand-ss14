using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.MedicalOps;

// Recovery went through an entity-target action: press, then find a body on screen and click it.
// The casualties are already known from the triage feed, so this lists them and pulls by name.
// Sits in the same band gap as Medical Command, stacked above it when a CMO has both.
public sealed class FSCasualtyPullController : UIController
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int MaxRows = 4;
    private const int RowWidth = 152;

    private static readonly Color PanelBg = FSPalette.PanelBack;
    private static readonly Color PanelBorder = FSPalette.PanelEdge;

    private PanelContainer? _frame;
    private BoxContainer? _rows;
    private HotbarGui? _hotbar;

    private readonly List<Button> _buttons = new();
    private readonly List<NetEntity> _targets = new();
    private Label? _status;

    public override void Initialize()
    {
        base.Initialize();

        var screenLoad = UIManager.GetUIController<GameplayStateLoadController>();
        screenLoad.OnScreenLoad += OnScreenLoad;
        screenLoad.OnScreenUnload += OnScreenUnload;
    }

    private void OnScreenLoad()
    {
        var screen = UIManager.ActiveScreen;
        if (screen == null)
            return;

        _buttons.Clear();
        _targets.Clear();

        _hotbar = FindWidget<HotbarGui>(screen);
        if (_hotbar?.Parent is not LayoutContainer container)
            return;

        _rows = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };

        var body = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
        };

        body.AddChild(new Label
        {
            Text = Loc.GetString("fs-casualty-pull-panel-title"),
            StyleClasses = { "LabelSubText" },
            HorizontalAlignment = Control.HAlignment.Center,
        });

        _status = new Label
        {
            StyleClasses = { "LabelSubText" },
            HorizontalAlignment = Control.HAlignment.Center,
        };

        body.AddChild(_rows);
        body.AddChild(_status);

        for (var i = 0; i < MaxRows; i++)
        {
            var index = i;
            var button = new Button
            {
                MinWidth = RowWidth,
                Visible = false,
                ToggleMode = false,
            };

            FSHudStyle.StyleButton(button, padX: 4, padY: 2);
            button.OnPressed += _ => Pull(index);

            _buttons.Add(button);
            _rows.AddChild(button);
        }

        _frame = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = PanelBg,
                BorderColor = PanelBorder,
                BorderThickness = new Thickness(1),
            },
            HorizontalAlignment = Control.HAlignment.Left,
            VerticalAlignment = Control.VAlignment.Top,
            Visible = false,
        };

        _frame.AddChild(body);
        LayoutContainer.SetAnchorPreset(_frame, LayoutContainer.LayoutPreset.TopLeft);
        container.AddChild(_frame);
    }

    private void OnScreenUnload()
    {
        _frame?.Orphan();
        _frame = null;
        _rows = null;
        _status = null;
        _hotbar = null;
        _buttons.Clear();
        _targets.Clear();
    }

    private void Pull(int index)
    {
        if (index >= _targets.Count)
            return;

        EntityManager.System<FSCasualtyPullClientSystem>().Request(_targets[index]);
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_frame == null || _status == null)
            return;

        if (_player.LocalEntity is not { } player
            || !EntityManager.TryGetComponent<FSCasualtyPullComponent>(player, out var pull))
        {
            _frame.Visible = false;
            return;
        }

        var board = EntityManager.System<FSCasualtyBoardSystem>();
        var remaining = pull.ReadyAt - _timing.CurTime;
        var ready = remaining <= TimeSpan.Zero;

        _targets.Clear();
        foreach (var entry in board.Entries)
        {
            if (_targets.Count >= MaxRows)
                break;

            if (entry.State is not (FSCasualtyState.Critical or FSCasualtyState.Dead))
                continue;

            if (EntityManager.GetEntity(entry.Patient) == player)
                continue;

            _targets.Add(entry.Patient);
        }

        // Nothing to recover is not an error state, so the panel steps out of the way entirely.
        if (_targets.Count == 0)
        {
            _frame.Visible = false;
            return;
        }

        _frame.Visible = true;

        for (var i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            if (i >= _targets.Count)
            {
                button.Visible = false;
                continue;
            }

            var entry = FindEntry(board, _targets[i]);
            button.Visible = true;
            button.Disabled = !ready;
            button.Text = entry?.Name ?? "?";
            button.Label.FontColorOverride = ready ? FSPalette.TextBright : FSPalette.TextDim;
        }

        _status.Text = ready
            ? Loc.GetString("fs-casualty-pull-panel-ready")
            : Loc.GetString("fs-casualty-pull-panel-cooldown",
                ("seconds", (int) Math.Ceiling(remaining.TotalSeconds)));

        _status.FontColorOverride = ready ? FSPalette.Ok : FSPalette.TextDim;

        UpdatePlacement();
    }

    private static FSCasualtyEntry? FindEntry(FSCasualtyBoardSystem board, NetEntity patient)
    {
        foreach (var entry in board.Entries)
        {
            if (entry.Patient == patient)
                return entry;
        }

        return null;
    }

    // Same gap as Medical Command. A CMO carries both panels, so this one stacks above it rather
    // than drawing straight through it.
    private const float VitalsRightEdge = 24f + 240f;
    private const float ActionBarFraction = 0.2995f;
    private const float ActionsHalfWidth = 114f;

    private void UpdatePlacement()
    {
        if (_frame == null || _hotbar == null)
            return;

        var screenW = _frame.Parent?.Size.X ?? 0f;
        if (screenW <= 0f)
            return;

        var actionsLeft = screenW * ActionBarFraction - ActionsHalfWidth;
        var gapCentre = (VitalsRightEdge + actionsLeft) * 0.5f;

        var size = _frame.DesiredSize;
        var x = MathF.Max(8f, gapCentre - size.X * 0.5f);
        var y = _hotbar.Position.Y + _hotbar.Size.Y - size.Y;

        if (_player.LocalEntity is { } player
            && EntityManager.HasComponent<FSCmoPanelComponent>(player))
        {
            y -= CmoPanelClearance;
        }

        LayoutContainer.SetPosition(_frame, new Vector2(x, y));
    }

    private const float CmoPanelClearance = 148f;

    private static T? FindWidget<T>(Control root) where T : Control
    {
        if (root is T match)
            return match;

        foreach (var child in root.Children)
        {
            if (FindWidget<T>(child) is { } found)
                return found;
        }

        return null;
    }
}
