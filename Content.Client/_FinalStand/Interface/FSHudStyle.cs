// Shared look for the HUD's panels and buttons, so every surface draws from one palette.
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.Interface;

public static class FSHudStyle
{
    public static readonly Color PanelBack = new(0.04f, 0.055f, 0.075f, 0.82f);
    public static readonly Color PanelEdge = new(0.47f, 0.55f, 0.65f, 0.22f);

    public static readonly Color TextMuted = Color.FromHex("#8fa1b3");
    public static readonly Color TextBright = Color.FromHex("#d8e0e8");

    private static readonly Color ButtonHoverBack = new(0.10f, 0.13f, 0.17f, 0.90f);
    private static readonly Color ButtonHoverEdge = new(0.47f, 0.55f, 0.65f, 0.45f);
    private static readonly Color ButtonPressBack = new(0.17f, 0.40f, 0.47f, 0.90f);
    private static readonly Color ButtonPressEdge = new(0.29f, 0.72f, 0.85f, 0.65f);

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
        var box = Box(new Color(0.02f, 0.03f, 0.04f, 0.85f), PanelEdge);
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
