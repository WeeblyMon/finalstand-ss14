using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._FinalStand.UI;

public sealed class FSMipmapRect : Control
{
    public string? TexturePath { get; set; }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        if (TexturePath == null
            || FSMipmaps.Pick(TexturePath, MathF.Min(PixelSize.X, PixelSize.Y)) is not { } texture)
            return;

        var fit = MathF.Min(PixelSize.X / (float) texture.Width, PixelSize.Y / (float) texture.Height);
        var size = (Vector2) texture.Size * fit;
        handle.DrawTextureRect(texture, UIBox2.FromDimensions(((Vector2) PixelSize - size) / 2f, size));
    }
}
