using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.UI;

public sealed class FSCroppedIcon : Control
{
    private static FSSpriteCrop? _crop;

    public SpriteSpecifier? Sprite { get; set; }
    public Color Tint { get; set; } = Color.White;

    protected override void Draw(DrawingHandleScreen handle)
    {
        if (Sprite == null)
            return;

        _crop ??= new FSSpriteCrop();
        _crop.Draw(handle, Sprite, (Vector2) PixelSize / 2f, MathF.Min(PixelWidth, PixelHeight), Tint);
    }
}
