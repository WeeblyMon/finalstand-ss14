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

    private static readonly Dictionary<string, (Texture Texture, Vector2 Offset)> Baked = new();

    public static void Arc(DrawingHandleScreen handle, Vector2 center, float radius, float width, float startDeg, float endDeg, Color color)
    {
        var r = MathF.Round(radius * 2f) / 2f;
        var w = MathF.Round(width * 4f) / 4f;
        var s = MathF.Round(startDeg * 2f) / 2f;
        var e = MathF.Round(endDeg * 2f) / 2f;
        var lo = MathF.Min(s, e) * MathF.PI / 180f;
        var hi = MathF.Max(s, e) * MathF.PI / 180f;
        var key = $"arc|{r}|{w}|{s}|{e}";
        DrawBaked(handle, center, color, key, (int) MathF.Ceiling(r * 2f) + 4, (int) MathF.Ceiling(r * 2f) + 4, (x, y) =>
        {
            var d = MathF.Sqrt(x * x + y * y);
            if (d > r || d < r - w)
                return false;
            var a = MathF.Atan2(y, x);
            while (a < lo) a += MathF.Tau;
            while (a > lo + MathF.Tau) a -= MathF.Tau;
            return a <= hi;
        }, trim: true);
    }

    public static void Slanted(DrawingHandleScreen handle, Vector2 topLeft, float width, float height, float slant, Color color,
        float border = 0f, float fill = 1f)
    {
        var w = MathF.Round(width);
        var h = MathF.Round(height);
        var sl = MathF.Round(slant);
        var b = MathF.Round(border * 2f) / 2f;
        var cut = MathF.Round(Math.Clamp(fill, 0f, 1f) * w);
        if (cut <= 0f)
            return;

        var key = $"slant|{w}|{h}|{sl}|{b}|{cut}";
        bool Inside(float x, float y, float inset)
        {
            var t = (y + h / 2f) / h;
            var left = sl * (1f - t) - w / 2f + inset;
            var right = w / 2f - sl * t - inset;
            return y >= -h / 2f + inset && y <= h / 2f - inset && x >= left && x <= right;
        }

        DrawBaked(handle, topLeft + new Vector2(w / 2f, h / 2f), color, key, (int) w + 2, (int) h + 2, (x, y) =>
            x + w / 2f <= cut && Inside(x, y, 0f) && (b <= 0f || !Inside(x, y, b)));
    }

    public static void Diamond(DrawingHandleScreen handle, Vector2 center, float size, Color color, float border = 0f)
    {
        var r = MathF.Round(size) / 2f;
        var b = MathF.Round(border * 2f) / 2f;
        DrawBaked(handle, center, color, $"diamond|{r}|{b}", (int) (r * 2f) + 4, (int) (r * 2f) + 4, (x, y) =>
        {
            var d = MathF.Abs(x) + MathF.Abs(y);
            return d <= r && (b <= 0f || d > r - b * 1.41f);
        });
    }

    private static void DrawBaked(DrawingHandleScreen handle, Vector2 center, Color color, string key, int width, int height,
        Func<float, float, bool> inside, bool trim = false)
    {
        if (color.A <= 0f)
            return;

        if (!Baked.TryGetValue(key, out var baked))
        {
            if (Baked.Count >= MaxCached)
            {
                foreach (var old in Baked.Values)
                    (old.Texture as OwnedTexture)?.Dispose();
                Baked.Clear();
            }

            baked = Bake(width, height, inside, trim);
            Baked[key] = baked;
        }

        var size = new Vector2(baked.Texture.Width, baked.Texture.Height);
        var topLeft = center + baked.Offset;
        handle.DrawTextureRect(baked.Texture, UIBox2.FromDimensions(new Vector2(MathF.Round(topLeft.X), MathF.Round(topLeft.Y)), size), color);
    }

    private static (Texture, Vector2) Bake(int width, int height, Func<float, float, bool> inside, bool trim)
    {
        _clyde ??= IoCManager.Resolve<IClyde>();
        var alpha = new byte[width * height];
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var hits = 0;
                for (var sy = 0; sy < Samples; sy++)
                for (var sx = 0; sx < Samples; sx++)
                {
                    if (inside(x + (sx + 0.5f) / Samples - width / 2f, y + (sy + 0.5f) / Samples - height / 2f))
                        hits++;
                }

                if (hits == 0)
                    continue;

                alpha[y * width + x] = (byte) (255 * hits / (Samples * Samples));
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
        }

        if (!trim || maxX < 0)
        {
            minX = 0; minY = 0; maxX = width - 1; maxY = height - 1;
        }

        var w = maxX - minX + 1;
        var h = maxY - minY + 1;
        using var image = new Image(w, h);
        var pixels = image.GetPixelSpan();
        for (var y = 0; y < h; y++)
        for (var x = 0; x < w; x++)
            pixels[y * w + x] = new Rgba32(255, 255, 255, alpha[(y + minY) * width + x + minX]);

        var texture = _clyde.LoadTextureFromImage(image, "fs-shape", new TextureLoadParameters
        {
            SampleParameters = new TextureSampleParameters { Filter = true },
            Srgb = true,
        });
        return (texture, new Vector2(minX - width / 2f, minY - height / 2f));
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
