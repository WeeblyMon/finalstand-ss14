using System.Numerics;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.Utility;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.PixelFormats;
using ISImage = SixLabors.ImageSharp.Image;

namespace Content.Client._FinalStand.UI;

// Draws sprite icons trimmed to their opaque pixels, so small items fill their slot like large ones.
public sealed class FSSpriteCrop
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IResourceCache _resources = default!;

    private readonly Dictionary<SpriteSpecifier, (Texture Texture, UIBox2 Region)> _cache = new();

    public FSSpriteCrop()
    {
        IoCManager.InjectDependencies(this);
    }

    public (Texture Texture, UIBox2 Region) Get(SpriteSpecifier sprite)
    {
        if (_cache.TryGetValue(sprite, out var cached))
            return cached;

        var texture = _entity.System<SpriteSystem>().Frame0(sprite);
        var full = new UIBox2(Vector2.Zero, texture.Size);
        var region = sprite switch
        {
            SpriteSpecifier.Rsi rsi => OpaqueBounds(SpriteSpecifierSerializer.TextureRoot / rsi.RsiPath / $"{rsi.RsiState}.png", texture.Size) ?? full,
            SpriteSpecifier.Texture tex => OpaqueBounds(SpriteSpecifierSerializer.TextureRoot / tex.TexturePath, texture.Size) ?? full,
            _ => full,
        };

        return _cache[sprite] = (texture, region);
    }

    public void Draw(DrawingHandleScreen handle, SpriteSpecifier sprite, Vector2 center, float size, Color modulate)
    {
        var (texture, region) = Get(sprite);
        var scale = size / MathF.Max(region.Width, region.Height);
        var extent = region.Size * scale;
        handle.DrawTextureRectRegion(texture, UIBox2.FromDimensions(center - extent / 2f, extent), region, modulate);
    }

    // Scans only the first frame: RSI state sheets lay frames out left to right from the top-left.
    private UIBox2? OpaqueBounds(ResPath path, Vector2i frame)
    {
        if (!_resources.TryContentFileRead(path, out var stream))
            return null;

        using (stream)
        using (var image = ISImage.Load<Rgba32>(stream))
        {
            var pixels = image.GetPixelSpan();
            int minX = frame.X, minY = frame.Y, maxX = -1, maxY = -1;
            var width = Math.Min(frame.X, image.Width);
            var height = Math.Min(frame.Y, image.Height);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    if (pixels[y * image.Width + x].A < 16)
                        continue;
                    minX = Math.Min(minX, x);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x);
                    maxY = Math.Max(maxY, y);
                }
            }

            return maxX < 0 ? null : new UIBox2(minX, minY, maxX + 1, maxY + 1);
        }
    }
}
