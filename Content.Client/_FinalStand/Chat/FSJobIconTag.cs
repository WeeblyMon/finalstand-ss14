using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Chat;

public sealed partial class FSJobIconTag : IMarkupTagHandler
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IEntityManager _entMan = default!;

    public string Name => "fsjobicon";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!node.Attributes.TryGetValue("job", out var job)
            || job.StringValue is not { } id
            || !_proto.TryIndex<JobIconPrototype>(id, out var icon))
            return false;

        var rect = new TextureRect
        {
            Texture = _entMan.System<SpriteSystem>().Frame0(icon.Icon),
            TextureScale = new Vector2(2f, 2f),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            VerticalAlignment = Control.VAlignment.Center,
            MouseFilter = Control.MouseFilterMode.Stop,
        };

        if (node.Attributes.TryGetValue("title", out var title) && !string.IsNullOrEmpty(title.StringValue))
            rect.ToolTip = title.StringValue;

        control = rect;
        return true;
    }
}
