using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSCmoPanelController : UIController
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private static readonly Color DirectiveIdle = Color.FromHex("#2E4A38");
    private static readonly Color DirectiveActive = Color.FromHex("#4FBF7A");
    private static readonly Color AbilityReady = Color.FromHex("#B5453A");
    private static readonly Color AbilityCooling = Color.FromHex("#4A2E2B");
    private static readonly Color PanelBg = Color.FromHex("#14171B");
    private static readonly Color PanelBorder = Color.FromHex("#2E333B");

    private const int PanelLeftMargin = 10;
    private const int PanelBottomGap = 78;

    private static readonly (FSCmoAbility Ability, string Loc)[] DirectiveSlots =
    {
        (FSCmoAbility.DirectiveTrauma, "fs-cmo-directive-trauma"),
        (FSCmoAbility.DirectivePharma, "fs-cmo-directive-pharma"),
        (FSCmoAbility.DirectiveFieldOps, "fs-cmo-directive-fieldops"),
    };

    private static readonly (FSCmoAbility Ability, string Loc)[] AbilitySlots =
    {
        (FSCmoAbility.MassCasualtyProtocol, "fs-cmo-mcp"),
        (FSCmoAbility.Mobilisation, "fs-cmo-mobilisation"),
    };

    private Control? _root;
    private PanelContainer? _frame;
    private readonly Dictionary<FSCmoAbility, Button> _buttons = new();
    private readonly Dictionary<FSCmoAbility, string> _labels = new();

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

        var rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
        };

        var header = new Label
        {
            Text = Loc.GetString("fs-cmo-panel-title"),
            StyleClasses = { "LabelSubText" },
            HorizontalAlignment = Control.HAlignment.Center,
        };
        rows.AddChild(header);

        rows.AddChild(BuildRow(DirectiveSlots, 92));
        rows.AddChild(BuildRow(AbilitySlots, 140));

        _frame = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = PanelBg,
                BorderColor = PanelBorder,
                BorderThickness = new Thickness(1),
            },
            HorizontalAlignment = Control.HAlignment.Left,
            VerticalAlignment = Control.VAlignment.Bottom,
            Margin = new Thickness(PanelLeftMargin, 0, 0, 0),
            Visible = false,
        };
        _frame.AddChild(rows);

        var spacer = new Control { VerticalExpand = true, MouseFilter = Control.MouseFilterMode.Ignore };

        var column = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };
        column.AddChild(spacer);
        column.AddChild(_frame);
        column.AddChild(new Control { SetHeight = PanelBottomGap, MouseFilter = Control.MouseFilterMode.Ignore });

        _root = column;
        LayoutContainer.SetAnchorPreset(_root, LayoutContainer.LayoutPreset.Wide);

        var target = IsSeparatedLayout() ? FindViewportContainer(screen) ?? (Control) screen : screen;
        target.AddChild(_root);
    }

    private bool IsSeparatedLayout()
    {
        return Enum.TryParse<ScreenType>(_cfg.GetCVar(CCVars.UILayout), out var layout)
               && layout == ScreenType.Separated;
    }

    private static Control? FindViewportContainer(Control screen)
    {
        foreach (var child in screen.Children)
        {
            if (child is LayoutContainer && child.Name == "ViewportContainer")
                return child;
        }

        return null;
    }

    private BoxContainer BuildRow((FSCmoAbility Ability, string Loc)[] slots, int width)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Margin = new Thickness(0, 3, 0, 0),
        };

        foreach (var (ability, loc) in slots)
        {
            var label = Loc.GetString($"{loc}-short");

            var button = new Button
            {
                Text = label,
                MinWidth = width,
                MinHeight = 30,
                Margin = new Thickness(2, 0),
                ToolTip = Loc.GetString($"{loc}-effects"),
            };

            var captured = ability;
            button.OnPressed += _ => EntityManager.System<FSCmoPanelSystem>().Request(captured);

            _labels[ability] = label;
            _buttons[ability] = button;
            row.AddChild(button);
        }

        return row;
    }

    private void OnScreenUnload()
    {
        _root?.Orphan();
        _root = null;
        _frame = null;
        _buttons.Clear();
        _labels.Clear();
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (_frame == null)
            return;

        if (_player.LocalEntity is not { } player
            || !EntityManager.TryGetComponent<FSCmoPanelComponent>(player, out var panel))
        {
            _frame.Visible = false;
            return;
        }

        _frame.Visible = true;

        foreach (var (ability, button) in _buttons)
        {
            var isDirective = DirectiveOf(ability) is { };

            var readyAt = ability switch
            {
                FSCmoAbility.MassCasualtyProtocol => panel.McpReadyAt,
                FSCmoAbility.Mobilisation => panel.MobilisationReadyAt,
                _ => panel.DirectiveReadyAt,
            };

            var remaining = readyAt - _timing.CurTime;
            var cooling = remaining > TimeSpan.Zero;

            button.Disabled = cooling;
            button.Text = cooling
                ? $"{_labels[ability]}  {Math.Ceiling(remaining.TotalSeconds):0}s"
                : _labels[ability];

            if (isDirective)
            {
                var active = DirectiveOf(ability) == panel.ActiveDirective;
                button.Modulate = active ? DirectiveActive : DirectiveIdle;
            }
            else
            {
                button.Modulate = cooling ? AbilityCooling : AbilityReady;
            }
        }
    }

    private static FSMedicalDirective? DirectiveOf(FSCmoAbility ability) => ability switch
    {
        FSCmoAbility.DirectiveTrauma => FSMedicalDirective.Trauma,
        FSCmoAbility.DirectivePharma => FSMedicalDirective.Pharma,
        FSCmoAbility.DirectiveFieldOps => FSMedicalDirective.FieldOps,
        _ => null,
    };
}
