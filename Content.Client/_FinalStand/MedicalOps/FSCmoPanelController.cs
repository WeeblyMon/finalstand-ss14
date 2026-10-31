using Robust.Shared.Utility;
using Robust.Client.ResourceManagement;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Client._FinalStand.WaveHud;
using System.Numerics;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

public sealed partial class FSCmoPanelController : UIController
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IResourceCache _resource = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private const int EdgePadding = 8;
    private const int SlotSize = 40;
    private const int IconSize = 24;
    private const int SlotGap = 3;
    private const int GroupGap = 10;

    private static readonly ResPath BoldFont = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private static readonly Color CoolingIcon = Color.White.WithAlpha(0.3f);

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
        (FSCmoAbility.Dividend, "fs-cmo-dividend"),
    };

    private static readonly StyleBoxFlat IdleBox = new()
    {
        BackgroundColor = FSPalette.ChipBack,
        BorderColor = FSPalette.ChipEdge,
        BorderThickness = new Thickness(1),
    };

    private static readonly StyleBoxFlat ReadyBox = new()
    {
        BackgroundColor = FSPalette.ChipBack,
        BorderColor = FSPalette.Money,
        BorderThickness = new Thickness(1),
    };

    private static readonly StyleBoxFlat LitBox = new()
    {
        BackgroundColor = FSPalette.TextBright.WithAlpha(0.14f),
        BorderColor = FSPalette.TextBright,
        BorderThickness = new Thickness(1),
    };

    private readonly record struct Slot(Button Button, Control Icon, Label Timer);

    private readonly Dictionary<FSCmoAbility, Slot> _slots = new();
    private readonly Dictionary<FSCmoAbility, int> _shownSeconds = new();

    private BoxContainer? _frame;

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

        _slots.Clear();
        _shownSeconds.Clear();

        if (FindWidget<HotbarGui>(screen)?.Parent is not LayoutContainer container)
            return;

        _frame = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = GroupGap,
            HorizontalAlignment = Control.HAlignment.Left,
            VerticalAlignment = Control.VAlignment.Top,
            Visible = false,
        };
        _frame.AddChild(BuildGroup("fs-cmo-group-directive", DirectiveSlots));
        _frame.AddChild(BuildGroup("fs-cmo-group-command", AbilitySlots));

        LayoutContainer.SetAnchorPreset(_frame, LayoutContainer.LayoutPreset.TopLeft);
        container.AddChild(_frame);
    }

    private static string AbilityIcon(FSCmoAbility ability)
    {
        var name = ability switch
        {
            FSCmoAbility.MassCasualtyProtocol => "mass_casualty",
            FSCmoAbility.Mobilisation => "mobilisation",
            FSCmoAbility.DirectiveTrauma => "trauma",
            FSCmoAbility.DirectivePharma => "pharma",
            FSCmoAbility.DirectiveFieldOps => "field_ops",
            _ => "dividend",
        };

        return $"/Textures/_FinalStand/Interface/CmoPanel/{name}.png";
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

    private Font Bold(int size)
    {
        return new VectorFont(_resource.GetResource<FontResource>(BoldFont), size);
    }

    private BoxContainer BuildGroup(string title, (FSCmoAbility Ability, string Loc)[] slots)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = SlotGap,
        };

        foreach (var (ability, loc) in slots)
        {
            row.AddChild(BuildSlot(ability, loc));
        }

        var group = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = SlotGap,
            VerticalAlignment = Control.VAlignment.Bottom,
        };
        group.AddChild(new Label
        {
            Text = Loc.GetString(title),
            FontOverride = Bold(8),
            FontColorOverride = FSPalette.TextMuted,
        });
        group.AddChild(row);
        return group;
    }

    private Button BuildSlot(FSCmoAbility ability, string loc)
    {
        var button = new Button
        {
            MinSize = new Vector2(SlotSize, SlotSize),
            SetSize = new Vector2(SlotSize, SlotSize),
            ToolTip = $"{Loc.GetString($"{loc}-name")}\n{Loc.GetString($"{loc}-effects")}",
        };

        FSHudStyle.StyleButton(button);

        var icon = new FSMipmapRect
        {
            TexturePath = AbilityIcon(ability),
            SetSize = new Vector2(IconSize, IconSize),
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
            MouseFilter = Control.MouseFilterMode.Ignore,
        };

        var timer = new Label
        {
            FontOverride = Bold(13),
            FontColorOverride = FSPalette.TextBright,
            HorizontalAlignment = Control.HAlignment.Center,
            VerticalAlignment = Control.VAlignment.Center,
            MouseFilter = Control.MouseFilterMode.Ignore,
            Visible = false,
        };

        button.AddChild(icon);
        button.AddChild(timer);

        var captured = ability;
        button.OnPressed += _ => EntityManager.System<FSCmoPanelSystem>().Request(captured);

        _slots[ability] = new Slot(button, icon, timer);
        return button;
    }

    private void OnScreenUnload()
    {
        _frame?.Orphan();
        _frame = null;
        _slots.Clear();
        _shownSeconds.Clear();

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

        UpdateSlots(panel);
        UpdatePlacement();
    }

    private void UpdateSlots(FSCmoPanelComponent panel)
    {
        foreach (var (ability, slot) in _slots)
        {
            var directive = DirectiveOf(ability);

            var readyAt = ability switch
            {
                FSCmoAbility.MassCasualtyProtocol => panel.McpReadyAt,
                FSCmoAbility.Mobilisation => panel.MobilisationReadyAt,
                FSCmoAbility.Dividend => panel.DividendReadyAt,
                _ => panel.DirectiveReadyAt,
            };

            if (ability == FSCmoAbility.Dividend)
                slot.Button.Visible = panel.DividendUnlocked;

            var remaining = readyAt - _timing.CurTime;
            var isActive = directive is { } active && active == panel.ActiveDirective;
            var cooling = remaining > TimeSpan.Zero && !isActive;

            slot.Button.Disabled = cooling;
            slot.Icon.Modulate = cooling ? CoolingIcon : Color.White;
            slot.Timer.Visible = cooling;

            var seconds = cooling ? (int) Math.Ceiling(remaining.TotalSeconds) : 0;
            if (!_shownSeconds.TryGetValue(ability, out var shown) || shown != seconds)
            {
                _shownSeconds[ability] = seconds;
                slot.Timer.Text = seconds.ToString();
            }

            slot.Button.StyleBoxOverride = isActive
                ? LitBox
                : directive == null && !cooling ? ReadyBox : IdleBox;
        }
    }

    private void UpdatePlacement()
    {
        if (_frame?.Parent is not { } parent
            || !_overlays.TryGetOverlay<WaveHudOverlay>(out var hud)
            || hud.VitalsBox is not { } vitals)
        {
            return;
        }

        var scale = parent.UIScale;
        var origin = parent.GlobalPixelPosition;
        var x = (vitals.Right - origin.X) / scale + EdgePadding;
        var y = (vitals.Bottom - origin.Y) / scale - _frame.DesiredSize.Y;

        LayoutContainer.SetPosition(_frame, new Vector2(x, y));
    }

    private static FSMedicalDirective? DirectiveOf(FSCmoAbility ability) => ability switch
    {
        FSCmoAbility.DirectiveTrauma => FSMedicalDirective.Trauma,
        FSCmoAbility.DirectivePharma => FSMedicalDirective.Pharma,
        FSCmoAbility.DirectiveFieldOps => FSMedicalDirective.FieldOps,
        _ => null,
    };
}
