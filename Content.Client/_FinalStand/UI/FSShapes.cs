// Anti-aliased discs and rings for screen UI: triangle geometry with a one-pixel alpha feather instead of the engine's jagged DrawCircle.
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.UI;

public static class FSShapes
{
    private const float Feather = 1f;
    private static DrawVertexUV2DColor[] _buffer = new DrawVertexUV2DColor[1536];

    public static void Disc(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        if (radius <= 0f || color.A <= 0f)
            return;

        var segments = Segments(radius);
        var solid = MathF.Max(0f, radius - Feather / 2f);
        var clear = color.WithAlpha(0f);
        var count = 0;
        Reserve(segments * 9);
        for (var i = 0; i < segments; i++)
        {
            var (a, b) = Edge(i, segments);
            Add(ref count, center, color);
            Add(ref count, center + a * solid, color);
            Add(ref count, center + b * solid, color);
            Quad(ref count, center, a, b, solid, radius + Feather / 2f, color, clear);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _buffer.AsSpan(0, count));
    }

    public static void Ring(DrawingHandleScreen handle, Vector2 center, float radius, float width, Color color)
    {
        if (radius <= 0f || width <= 0f || color.A <= 0f)
            return;

        var segments = Segments(radius);
        var outer = radius;
        var inner = MathF.Max(0f, radius - width);
        var core = MathF.Max(0f, width - Feather) / 2f;
        var mid = (outer + inner) / 2f;
        var alpha = color.WithAlpha(color.A * MathF.Min(1f, width / Feather));
        var clear = color.WithAlpha(0f);
        var count = 0;
        Reserve(segments * 18);
        for (var i = 0; i < segments; i++)
        {
            var (a, b) = Edge(i, segments);
            Quad(ref count, center, a, b, MathF.Max(0f, mid - core - Feather / 2f), mid - core, clear, alpha);
            Quad(ref count, center, a, b, mid - core, mid + core, alpha, alpha);
            Quad(ref count, center, a, b, mid + core, mid + core + Feather / 2f, alpha, clear);
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _buffer.AsSpan(0, count));
    }

    private static int Segments(float radius)
    {
        return Math.Clamp((int) (radius * 0.8f), 24, 160);
    }

    private static (Vector2 A, Vector2 B) Edge(int i, int segments)
    {
        var t0 = MathF.Tau * i / segments;
        var t1 = MathF.Tau * (i + 1) / segments;
        return (new Vector2(MathF.Cos(t0), MathF.Sin(t0)), new Vector2(MathF.Cos(t1), MathF.Sin(t1)));
    }

    private static void Quad(ref int count, Vector2 center, Vector2 a, Vector2 b, float r0, float r1, Color c0, Color c1)
    {
        Add(ref count, center + a * r0, c0);
        Add(ref count, center + a * r1, c1);
        Add(ref count, center + b * r1, c1);
        Add(ref count, center + a * r0, c0);
        Add(ref count, center + b * r1, c1);
        Add(ref count, center + b * r0, c0);
    }

    private static void Add(ref int count, Vector2 position, Color color)
    {
        _buffer[count++] = new DrawVertexUV2DColor(position, color);
    }

    private static void Reserve(int count)
    {
        if (_buffer.Length < count)
            _buffer = new DrawVertexUV2DColor[count];
    }
}
