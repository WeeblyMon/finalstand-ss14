using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.UI;

public static class FSWorldChip
{
    public static readonly Color Back = Color.FromHex("#0b0a0a").WithAlpha(0.8f);
    public static readonly Color Track = Color.White.WithAlpha(0.08f);
    public static readonly Color Trail = Color.White.WithAlpha(0.85f);

    public static float Width(float width, float height, int segments, float gap)
    {
        var seg = SegmentWidth(width, height, segments, gap);
        return seg * segments + gap * (segments - 1) + height * 0.6f;
    }

    public static void Bar(DrawingHandleScreen handle, Vector2 topLeft, float width, float height, int segments, float gap,
        float fraction, Color fill, float trail = 0f)
    {
        var slant = MathF.Round(height * 0.6f);
        var seg = SegmentWidth(width, height, segments, gap);
        var total = seg * segments + gap * (segments - 1) + slant;
        FSShapes.Slanted(handle, topLeft - Vector2.One, total + 2f, height + 2f, slant, Back);

        for (var i = 0; i < segments; i++)
        {
            var at = topLeft + new Vector2(i * (seg + gap), 0f);
            FSShapes.Slanted(handle, at, seg + slant, height, slant, Track);
            Segment(handle, at, seg, slant, height, trail * segments - i, Trail);
            Segment(handle, at, seg, slant, height, fraction * segments - i, fill);
        }
    }

    public static void Rail(DrawingHandleScreen handle, Vector2 topLeft, float width, float height, float fraction, Color fill)
    {
        var slant = MathF.Round(height);
        FSShapes.Slanted(handle, topLeft - Vector2.One, width + 2f, height + 2f, slant, Back);
        FSShapes.Slanted(handle, topLeft, width, height, slant, Track);
        FSShapes.Slanted(handle, topLeft, width, height, slant, fill, fill: fraction);
    }

    public static void Pips(DrawingHandleScreen handle, Vector2 rightCenter, int count, float size, Color color)
    {
        for (var i = 0; i < count; i++)
        {
            var center = rightCenter - new Vector2(size * 0.5f + i * size * 0.8f, 0f);
            FSShapes.Diamond(handle, center, size + 2f, Back);
            FSShapes.Diamond(handle, center, size, color);
        }
    }

    private static float SegmentWidth(float width, float height, int segments, float gap)
    {
        var slant = MathF.Round(height * 0.6f);
        return MathF.Max(2f, MathF.Round((width - slant - gap * (segments - 1)) / segments));
    }

    private static void Segment(DrawingHandleScreen handle, Vector2 at, float seg, float slant, float height, float part, Color color)
    {
        if (part <= 0f)
            return;

        var cut = Math.Clamp(part, 0f, 1f);
        FSShapes.Slanted(handle, at, seg + slant, height, slant, color, fill: cut >= 1f ? 1f : (slant + seg * cut) / (seg + slant));
    }
}
