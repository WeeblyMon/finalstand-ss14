// Shared look for the bottom band's module panels, matching the wave overlay's own panel colours.
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Interface;

public static class FSHudStyle
{
    public static readonly Color PanelBack = new(0.04f, 0.055f, 0.075f, 0.82f);
    public static readonly Color PanelEdge = new(0.47f, 0.55f, 0.65f, 0.22f);

    public static StyleBoxFlat ModulePanel()
    {
        var box = new StyleBoxFlat
        {
            BackgroundColor = PanelBack,
            BorderColor = PanelEdge,
            BorderThickness = new Thickness(1),
        };
        box.SetContentMarginOverride(StyleBox.Margin.Horizontal, 8);
        box.SetContentMarginOverride(StyleBox.Margin.Vertical, 7);
        return box;
    }
}
