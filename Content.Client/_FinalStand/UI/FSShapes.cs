// Anti-aliased discs and rings for screen UI: triangle geometry with a gamma-corrected alpha fade instead of the engine's jagged DrawCircle.
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.UI;

public static class FSShapes
{
    private const float Feather = 1.6f;
    private const int FeatherSteps = 4;
    private static DrawVertexUV2DColor[] _buffer = new DrawVertexUV2DColor[4096];
    private static readonly List<(float Radius, float Alpha)> Stops = new();

    public static void Disc(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
    {
        if (radius <= 0f || color.A <= 0f)
            return;

        Stops.Clear();
        var edge = MathF.Max(0f, radius - Feather / 2f);
        Stops.Add((0f, 1f));
        Fade(edge, radius + Feather / 2f, false);
        Draw(handle, center, radius, color, fan: true);
    }

    public static void Ring(DrawingHandleScreen handle, Vector2 center, float radius, float width, Color color)
    {
        if (radius <= 0f || width <= 0f || color.A <= 0f)
            return;

        Stops.Clear();
        var inner = MathF.Max(0f, radius - width);
        var strength = MathF.Min(1f, width / Feather);
        Fade(MathF.Max(0f, inner - Feather / 2f), inner + Feather / 2f, true);
        Fade(radius - Feather / 2f, radius + Feather / 2f, false);
        for (var i = 0; i < Stops.Count; i++)
            Stops[i] = (Stops[i].Radius, Stops[i].Alpha * strength);
        Draw(handle, center, radius, color, fan: false);
    }

    private static void Fade(float from, float to, bool rising)
    {
        for (var k = 0; k <= FeatherSteps; k++)
        {
            var t = (float) k / FeatherSteps;
            var coverage = rising ? t : 1f - t;
            Stops.Add((from + (to - from) * t, MathF.Pow(coverage, 2.2f)));
        }
    }

    private static void Draw(DrawingHandleScreen handle, Vector2 center, float radius, Color color, bool fan)
    {
        var segments = Math.Clamp((int) (radius * 1.2f), 32, 256);
        var bands = Stops.Count - 1;
        Reserve(segments * bands * 6);
        var count = 0;
        for (var i = 0; i < segments; i++)
        {
            var t0 = MathF.Tau * i / segments;
            var t1 = MathF.Tau * (i + 1) / segments;
            var a = new Vector2(MathF.Cos(t0), MathF.Sin(t0));
            var b = new Vector2(MathF.Cos(t1), MathF.Sin(t1));
            for (var s = 0; s < bands; s++)
            {
                var (r0, a0) = Stops[s];
                var (r1, a1) = Stops[s + 1];
                if (r1 <= r0 || a0 <= 0f && a1 <= 0f)
                    continue;

                var c0 = color.WithAlpha(color.A * a0);
                var c1 = color.WithAlpha(color.A * a1);
                if (fan && s == 0)
                {
                    Add(ref count, center, c0);
                    Add(ref count, center + a * r1, c1);
                    Add(ref count, center + b * r1, c1);
                    continue;
                }

                Add(ref count, center + a * r0, c0);
                Add(ref count, center + a * r1, c1);
                Add(ref count, center + b * r1, c1);
                Add(ref count, center + a * r0, c0);
                Add(ref count, center + b * r1, c1);
                Add(ref count, center + b * r0, c0);
            }
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleList, Texture.White, _buffer.AsSpan(0, count));
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
