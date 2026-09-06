using System.Diagnostics.CodeAnalysis;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Chat;

public sealed class FSAbilityTag : IMarkupTagHandler
{
    private static readonly Color UnderlineColor = Color.FromHex("#7FD8A8");

    public string Name => "fsability";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;

        if (!node.Attributes.TryGetValue("name", out var name) || name.StringValue == null)
            return false;

        var label = new Label
        {
            Text = name.StringValue,
            Modulate = UnderlineColor,
        };

        var underline = new PanelContainer
        {
            SetHeight = 1,
            PanelOverride = new StyleBoxFlat { BackgroundColor = UnderlineColor },
        };

        var stack = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            MouseFilter = Control.MouseFilterMode.Stop,
            VerticalAlignment = Control.VAlignment.Center,
        };
        stack.AddChild(label);
        stack.AddChild(underline);

        if (node.Attributes.TryGetValue("tooltip", out var tooltip) && tooltip.StringValue != null)
            stack.ToolTip = tooltip.StringValue;

        control = stack;
        return true;
    }
}
