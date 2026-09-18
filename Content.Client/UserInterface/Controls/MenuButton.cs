using Content.Client._FinalStand.Interface;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Controls;

public sealed partial class MenuButton : ContainerButton
{
    [Dependency] private IInputManager _inputManager = default!;
    public const string StyleClassLabelTopButton = "topButtonLabel";

    // TODO: KIIIIIILLLLLLLLLLLLLLLLLLLLLLLLLLL --kaylie.
    private static readonly Color ColorNormal = FSPalette.TextBright;
    private static readonly Color ColorHovered = FSPalette.TextBright;
    private static readonly Color ColorPressed = FSPalette.TextDim;

    private const float VertPad = 0f;

    // FINALSTAND: the vanilla chips are a nine-patch texture on the old palette.
    private static readonly StyleBoxFlat ChipNormal = MakeChip(FSPalette.ChipBack, FSPalette.ChipEdge);
    private static readonly StyleBoxFlat ChipHovered = MakeChip(FSPalette.ButtonHoverBack, FSPalette.ButtonHoverEdge);
    private static readonly StyleBoxFlat ChipPressed = MakeChip(FSPalette.ButtonPressBack, FSPalette.ButtonPressEdge);

    private static StyleBoxFlat MakeChip(Color fill, Color edge)
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = fill,
            BorderColor = edge,
            BorderThickness = new Thickness(1),
        };
        box.SetContentMarginOverride(StyleBox.Margin.All, 0);
        return box;
    }

    private BoundKeyFunction? _function;
    private readonly BoxContainer _root;
    private readonly TextureRect? _buttonIcon;
    private readonly Label? _buttonLabel;

    public string AppendStyleClass { set => AddStyleClass(value); }
    public Texture? Icon { get => _buttonIcon!.Texture; set => _buttonIcon!.Texture = value; }

    public BoundKeyFunction? BoundKey
    {
        get => _function;
        set
        {
            _function = value;
            _buttonLabel!.Text = _function == null ? "" : BoundKeyHelper.ShortKeyName(_function.Value);
        }
    }

    public BoxContainer ButtonRoot => _root;

    public MenuButton()
    {
        IoCManager.InjectDependencies(this);
        // FINALSTAND: the bar is keybind chips, not icon buttons - the icon is what forced it tall.
        _buttonIcon = new TextureRect()
        {
            TextureScale = new Vector2(0.35f, 0.35f),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            VerticalExpand = true,
            Margin = new Thickness(0, VertPad),
            ModulateSelfOverride = ColorNormal,
            Stretch = TextureRect.StretchMode.KeepCentered,
            Visible = false
        };
        _buttonLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HAlignment.Center,
            ModulateSelfOverride = ColorNormal,
            StyleClasses = {StyleClassLabelTopButton}
        };
        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Children =
            {
                _buttonIcon,
                _buttonLabel
            }
        };
        AddChild(_root);
        ToggleMode = true;
        StyleBoxOverride = ChipNormal;
    }

    protected override void EnteredTree()
    {
        _inputManager.OnKeyBindingAdded += OnKeyBindingChanged;
        _inputManager.OnKeyBindingRemoved += OnKeyBindingChanged;
        _inputManager.OnInputModeChanged += OnKeyBindingChanged;
    }

    protected override void ExitedTree()
    {
        _inputManager.OnKeyBindingAdded -= OnKeyBindingChanged;
        _inputManager.OnKeyBindingRemoved -= OnKeyBindingChanged;
        _inputManager.OnInputModeChanged -= OnKeyBindingChanged;
    }

    private void OnKeyBindingChanged(IKeyBinding obj)
    {
        _buttonLabel!.Text = _function == null ? "" : BoundKeyHelper.ShortKeyName(_function.Value);
    }

    private void OnKeyBindingChanged()
    {
        _buttonLabel!.Text = _function == null ? "" : BoundKeyHelper.ShortKeyName(_function.Value);
    }

    protected override void StylePropertiesChanged()
    {
        base.StylePropertiesChanged();
        UpdateChildColors();
    }

    private void SetChip(StyleBoxFlat box)
    {
        if (!ReferenceEquals(StyleBoxOverride, box))
            StyleBoxOverride = box;
    }

    private void UpdateChildColors()
    {
        if (_buttonIcon == null || _buttonLabel == null) return;
        switch (DrawMode)
        {
            case DrawModeEnum.Normal:
                _buttonIcon.ModulateSelfOverride = ColorNormal;
                _buttonLabel.ModulateSelfOverride = ColorNormal;
                SetChip(ChipNormal);
                break;

            case DrawModeEnum.Pressed:
                _buttonIcon.ModulateSelfOverride = Color.White;
                _buttonLabel.ModulateSelfOverride = Color.White;
                SetChip(ChipPressed);
                break;

            case DrawModeEnum.Hover:
                _buttonIcon.ModulateSelfOverride = ColorHovered;
                _buttonLabel.ModulateSelfOverride = ColorHovered;
                SetChip(ChipHovered);
                break;

            case DrawModeEnum.Disabled:
                break;
        }
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();
        UpdateChildColors();
    }
}
