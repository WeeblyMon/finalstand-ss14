// Shared look for the HUD's panels and buttons, so every surface draws from one palette.
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.Interface;

public static class FSHudStyle
{
    public static Color PanelBack => FSPalette.PanelBack;
    public static Color PanelEdge => FSPalette.PanelEdge;

    public static Color TextMuted => FSPalette.TextMuted;
    public static Color TextBright => FSPalette.TextBright;

    private static Color ButtonHoverBack => FSPalette.ButtonHoverBack;
    private static Color ButtonHoverEdge => FSPalette.ButtonHoverEdge;
    private static Color ButtonPressBack => FSPalette.ButtonPressBack;
    private static Color ButtonPressEdge => FSPalette.ButtonPressEdge;

    public static StyleBoxFlat ModulePanel()
    {
        var box = Box(PanelBack, PanelEdge);
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 7);
        return box;
    }

    private static StyleBoxFlat Box(Color fill, Color edge)
    {
        return new StyleBoxFlat
        {
            BackgroundColor = fill,
            BorderColor = edge,
            BorderThickness = new Thickness(1),
        };
    }

    /// <summary>Flat field for text entry: darker than a panel so it reads as writable.</summary>
    public static StyleBoxFlat InputBox()
    {
        var box = Box(FSPalette.PanelDeep, PanelEdge);
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 6);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 3);
        return box;
    }

    /// <summary>
    /// Puts a button on the HUD palette. ContainerButton carries a single style box rather than one
    /// per state, so hover and press are swapped on the mouse events instead.
    /// </summary>
    public static void StyleButton(ContainerButton button, int padX = 6, int padY = 3)
    {
        var normal = Box(PanelBack, PanelEdge);
        var hover = Box(ButtonHoverBack, ButtonHoverEdge);
        var pressed = Box(ButtonPressBack, ButtonPressEdge);

        foreach (var box in new[] { normal, hover, pressed })
        {
            box.SetContentMarginOverride(StyleBox.Margin.Horizontal, padX);
            box.SetContentMarginOverride(StyleBox.Margin.Vertical, padY);
        }

        button.StyleBoxOverride = normal;

        if (button is Button labelled)
            labelled.Label.ModulateSelfOverride = TextMuted;

        button.OnMouseEntered += _ =>
            button.StyleBoxOverride = button.Pressed ? pressed : hover;
        button.OnMouseExited += _ =>
            button.StyleBoxOverride = button.Pressed ? pressed : normal;
        button.OnToggled += args =>
            button.StyleBoxOverride = args.Pressed ? pressed : normal;
    }
}
