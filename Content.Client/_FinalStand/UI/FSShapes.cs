// Smooth discs and rings for screen UI. Each size is rendered once into a texture at its exact pixel size, with
// supersampled coverage, then drawn 1:1 so nothing is scaled or resampled. Shrunk does the same for a PNG: a one-off
// high-quality resize to the size it is shown at, standing in for the mipmaps the engine doesn't have.
using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.Processing;
using Robust.Client.Utility;
using Robust.Shared.Graphics;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;
using Image = SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>;

namespace Content.Client._FinalStand.UI;

public static class FSShapes
{
    private const int Samples = 4;
    private const float Step = 4f;
    private const int MaxCached = 1024;

    private static readonly Dictionary<(int Radius, int Width), Texture> Cache = new();
    private static readonly Dictionary<(string Path, int Size), Texture> Shrinks = new();
    private static IClyde? _clyde;

    public static void Disc(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        Draw(handle, center, radius, 0f, color);
    }

    public static void Ring(DrawingHandleScreen handle, Vector2 center, float radius, float width, Color color)
    {
        Draw(handle, center, radius, MathF.Max(0.5f, width), color);
    }

    private static void Draw(DrawingHandleScreen handle, Vector2 center, float radius, float width, Color color)
    {
        if (radius <= 0.5f || color.A <= 0f)
            return;

        var key = ((int) MathF.Round(radius * Step), (int) MathF.Round(width * Step));
        if (!Cache.TryGetValue(key, out var texture))
        {
            if (Cache.Count >= MaxCached)
            {
                foreach (var old in Cache.Values)
                    (old as OwnedTexture)?.Dispose();
                Cache.Clear();
            }
            texture = Render(key.Item1 / Step, key.Item2 / Step);
            Cache[key] = texture;
        }

        var size = new Vector2(texture.Width, texture.Height);
        var topLeft = new Vector2(MathF.Round(center.X - size.X / 2f), MathF.Round(center.Y - size.Y / 2f));
        handle.DrawTextureRect(texture, UIBox2.FromDimensions(topLeft, size), color);
    }

    public static Texture? Shrunk(string path, int pixels)
    {
        if (Shrinks.TryGetValue((path, pixels), out var cached))
            return cached;

        _clyde ??= IoCManager.Resolve<IClyde>();
        if (!IoCManager.Resolve<IResourceManager>().TryContentFileRead(new ResPath(path), out var stream))
            return null;

        using (stream)
        using (var image = SixLabors.ImageSharp.Image.Load<Rgba32>(stream))
        using (var resized = image.Clone(x => x.Resize(pixels, pixels)))
        {
            var texture = _clyde.LoadTextureFromImage(resized, path, new TextureLoadParameters
            {
                SampleParameters = new TextureSampleParameters { Filter = true },
                Srgb = true,
            });
            Shrinks[(path, pixels)] = texture;
            return texture;
        }
    }

    private static Texture Render(float radius, float width)
    {
        _clyde ??= IoCManager.Resolve<IClyde>();
        var side = (int) MathF.Ceiling(radius * 2f) + 4;
        var mid = side / 2f;
        var inner = width <= 0f ? -1f : radius - width;
        using var image = new Image(side, side);
        var pixels = image.GetPixelSpan();
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var hits = 0;
                for (var sy = 0; sy < Samples; sy++)
                for (var sx = 0; sx < Samples; sx++)
                {
                    var dx = x + (sx + 0.5f) / Samples - mid;
                    var dy = y + (sy + 0.5f) / Samples - mid;
                    var d = MathF.Sqrt(dx * dx + dy * dy);
                    if (d <= radius && d >= inner)
                        hits++;
                }

                pixels[y * side + x] = new Rgba32(255, 255, 255, (byte) (255 * hits / (Samples * Samples)));
            }
        }

        var parameters = new TextureLoadParameters
        {
            SampleParameters = new TextureSampleParameters { Filter = true },
            Srgb = true,
        };
        return _clyde.LoadTextureFromImage(image, "fs-shape", parameters);
    }
}
