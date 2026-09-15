using Robust.Shared.Utility;
using Robust.Client.ResourceManagement;
using Content.Client._FinalStand.Interface;
using System.Numerics;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Client.UserInterface.Systems.Inventory.Widgets;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSCmoPanelController : UIController
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _resource = default!;

    private static readonly Color DirectiveIdle = Color.FromHex("#2E4A38");
    private static readonly Color DirectiveActive = Color.FromHex("#4FBF7A");
    private static readonly Color AbilityReady = Color.FromHex("#B5453A");
    private static readonly Color AbilityCooling = Color.FromHex("#4A2E2B");
    // FINALSTAND: was its own #14171B/#2E333B pair, opaque while every other panel is 82%.
    // Mirrors the band layout in HotbarGui/DefaultGameScreen: hands at the centre line, storage at
    // 0.6703, so the empty gap between them is centred here.
    private const float HandsFraction = 0.5f;
    private const float StorageFraction = 0.6703f;
    private const float BandGapFraction = (HandsFraction + StorageFraction) / 2f;
    private const float HandsHalfWidth = 74f;
    private const float StorageHalfWidth = 56f;

    private static readonly Color PanelBg = FSHudStyle.PanelBack;
    private static readonly Color PanelBorder = FSHudStyle.PanelEdge;

    private const int EdgePadding = 8;
    private const int WideDirectiveWidth = 92;
    private const int WideAbilityWidth = 140;
    private const int NarrowWidth = 116;
    private const int WideLayoutMinimum = 310;

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

    private readonly Dictionary<FSCmoAbility, int> _shownSeconds = new();

    private PanelContainer? _frame;
    private BoxContainer? _directiveRow;
    private BoxContainer? _abilityRow;
    private InventoryGui? _inventory;
    private HotbarGui? _hotbar;
    private bool _narrow;

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
        _labels.Clear();

        _hotbar = FindWidget<HotbarGui>(screen);
        _inventory = FindWidget<InventoryGui>(screen);

        if (_hotbar?.Parent is not LayoutContainer container)
            return;

        _directiveRow = BuildRow(DirectiveSlots, WideDirectiveWidth);
        _abilityRow = BuildRow(AbilitySlots, WideAbilityWidth);

        var rows = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(6),
        };
        rows.AddChild(new Label
        {
            Text = Loc.GetString("fs-cmo-panel-title"),
            StyleClasses = { "LabelSubText" },
            HorizontalAlignment = Control.HAlignment.Center,
        });
        rows.AddChild(_directiveRow);
        rows.AddChild(_abilityRow);

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
        _frame.AddChild(rows);

        LayoutContainer.SetAnchorPreset(_frame, LayoutContainer.LayoutPreset.TopLeft);
        container.AddChild(_frame);
    }

    // 6px inset + a 16px icon + 6px of air. At 18 the label slid under the icon.
    private const int IconGutter = 28;

    private static ResPath? AbilityIcon(FSCmoAbility ability)
    {
        var name = ability switch
        {
            FSCmoAbility.MassCasualtyProtocol => "mass_casualty",
            FSCmoAbility.Mobilisation => "mobilisation",
            FSCmoAbility.DirectiveTrauma => "trauma",
            FSCmoAbility.DirectivePharma => "pharma",
            FSCmoAbility.DirectiveFieldOps => "field_ops",
            _ => null,
        };

        return name is null ? null : new ResPath($"/Textures/_FinalStand/Interface/CmoPanel/{name}.png");
    }

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

            FSHudStyle.StyleButton(button);

            // Icon on the left, label centred. ContainerButton arranges every child to the full
            // rect, so alignment alone places them - no extra container needed, and the whole
            // button stays the click target.
            if (AbilityIcon(ability) is { } iconPath
                && _resource.TryGetResource<TextureResource>(iconPath, out var icon))
            {
                button.AddChild(new TextureRect
                {
                    Texture = icon,
                    HorizontalAlignment = Control.HAlignment.Left,
                    VerticalAlignment = Control.VAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0),
                    MouseFilter = Control.MouseFilterMode.Ignore,
                });

                // Left-aligned, not centred: a centred label on the wider entries slid back under
                // the icon.
                button.Label.Align = Label.AlignMode.Left;
                button.Label.Margin = new Thickness(IconGutter, 0, 0, 0);
            }

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
        _frame?.Orphan();
        _frame = null;
        _directiveRow = null;
        _abilityRow = null;
        _inventory = null;
        _hotbar = null;
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

        UpdateButtons(panel);
        UpdatePlacement();
    }

    private void UpdateButtons(FSCmoPanelComponent panel)
    {
        foreach (var (ability, button) in _buttons)
        {
            var directive = DirectiveOf(ability);

            var readyAt = ability switch
            {
                FSCmoAbility.MassCasualtyProtocol => panel.McpReadyAt,
                FSCmoAbility.Mobilisation => panel.MobilisationReadyAt,
                _ => panel.DirectiveReadyAt,
            };

            var remaining = readyAt - _timing.CurTime;
            var cooling = remaining > TimeSpan.Zero;

            // Standing down the active directive must stay available during its own cooldown,
            // otherwise the button that cancels it is greyed out for the whole duration.
            var isActive = directive is { } active && active == panel.ActiveDirective;
            button.Disabled = cooling && !isActive;

            // The label only changes once a second, so rebuilding it per frame would allocate a
            // string and invalidate layout for every button on every frame.
            var seconds = cooling ? (int) Math.Ceiling(remaining.TotalSeconds) : 0;
            if (!_shownSeconds.TryGetValue(ability, out var shown) || shown != seconds)
            {
                _shownSeconds[ability] = seconds;
                button.Text = cooling
                    ? $"{_labels[ability]}  {seconds:0}s"
                    : _labels[ability];
            }

            if (directive is { } value)
                button.Label.FontColorOverride = value == panel.ActiveDirective ? DirectiveActive : DirectiveIdle;
            else
                button.Label.FontColorOverride = cooling ? AbilityCooling : AbilityReady;
        }
    }

    private void UpdatePlacement()
    {
        if (_frame == null || _hotbar == null || _directiveRow == null || _abilityRow == null)
            return;

        // FINALSTAND: the hotbar widget spans the whole band now, so its Position.X is 0 and the
        // old "gap between inventory and hotbar" arithmetic produced a negative width. The panel
        // instead sits in the band's own gap, between the centred hands and the storage cluster.
        var screenW = _frame.Parent?.Size.X ?? 0f;
        if (screenW <= 0f)
            return;

        var gapCentre = screenW * BandGapFraction;
        var available = screenW * (StorageFraction - HandsFraction) - HandsHalfWidth - StorageHalfWidth;

        SetNarrow(available < WideLayoutMinimum);

        var size = _frame.DesiredSize;
        var x = MathF.Max(EdgePadding, gapCentre - size.X * 0.5f);
        var y = _hotbar.Position.Y + _hotbar.Size.Y - size.Y;

        LayoutContainer.SetPosition(_frame, new Vector2(x, y));
    }

    private void SetNarrow(bool narrow)
    {
        if (narrow == _narrow || _directiveRow == null || _abilityRow == null)
            return;

        _narrow = narrow;

        var orientation = narrow
            ? BoxContainer.LayoutOrientation.Vertical
            : BoxContainer.LayoutOrientation.Horizontal;

        _directiveRow.Orientation = orientation;
        _abilityRow.Orientation = orientation;

        foreach (var (ability, button) in _buttons)
        {
            if (narrow)
                button.MinWidth = NarrowWidth;
            else
                button.MinWidth = DirectiveOf(ability) is null ? WideAbilityWidth : WideDirectiveWidth;

            button.Margin = narrow ? new Thickness(0, 1) : new Thickness(2, 0);
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
