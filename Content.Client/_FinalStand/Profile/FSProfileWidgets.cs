using Content.Client._FinalStand.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._FinalStand.Profile;

public static class FSProfileWidgets
{
    public static PanelContainer Card(Control child, Color border, Color? back = null)
    {
        var panel = new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = back ?? FSUiPalette.BgSurface,
                BorderColor = border,
                BorderThickness = new Thickness(1),
            },
        };
        panel.AddChild(child);
        return panel;
    }

    public static Label Text(string text, Font? font, Color color, bool expand = false)
    {
        return new Label
        {
            Text = text,
            FontOverride = font,
            FontColorOverride = color,
            HorizontalExpand = expand,
            VerticalAlignment = Control.VAlignment.Center,
        };
    }

    public static RichTextLabel Wrapped(string text, Color color, float maxWidth)
    {
        var label = new RichTextLabel { MaxWidth = maxWidth, HorizontalAlignment = Control.HAlignment.Left };
        label.SetMessage(Robust.Shared.Utility.FormattedMessage.FromUnformatted(text), defaultColor: color);
        return label;
    }

    public static ProgressBar Bar(float value, Color fill)
    {
        return new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            Value = Math.Clamp(value, 0f, 1f),
            MinHeight = 6,
            HorizontalExpand = true,
            BackgroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = FSUiPalette.BgTrack },
            ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = fill },
        };
    }
}
