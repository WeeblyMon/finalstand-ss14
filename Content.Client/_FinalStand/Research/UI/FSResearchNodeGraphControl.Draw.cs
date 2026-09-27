using System;
using System.Collections.Generic;
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Research.UI;

public sealed partial class FSResearchNodeGraphControl
{
    private static readonly Color Paper = Color.FromHex("#0e0d0c");
    private static readonly Color GridMinor = Color.FromHex("#171514");
    private static readonly Color GridMajor = Color.FromHex("#1d1b19");
    private static readonly Color Olive = Color.FromHex("#7e9464");
    private static readonly Color OliveLine = Color.FromHex("#6f8558");
    private static readonly Color OliveText = Color.FromHex("#9bb07f");
    private static readonly Color OliveFill = Color.FromHex("#1a2016");
    private static readonly Color Gold = Color.FromHex("#d2a44e");
    private static readonly Color GoldLight = Color.FromHex("#e6c07a");
    private static readonly Color Amber = Color.FromHex("#c08a3e");
    private static readonly Color AmberDark = Color.FromHex("#29221a");
    private static readonly Color AmberEdge = Color.FromHex("#5c4a26");
    private static readonly Color OffWhite = Color.FromHex("#dfdcd7");
    private static readonly Color Soft = Color.FromHex("#b9b5ae");
    private static readonly Color Muted = Color.FromHex("#96938d");
    private static readonly Color Dim = Color.FromHex("#6a6761");
    private static readonly Color Faint = Color.FromHex("#3a3734");
    private static readonly Color Track = Color.FromHex("#2a2826");
    private static readonly Color DeadLine = Color.FromHex("#4a4540");
    private static readonly Color BandFill = Color.FromHex("#121110");
    private static readonly Color RustDark = Color.FromHex("#5a2c24");
    private static readonly Color NodeFill = Color.FromHex("#1a1816");
    private static readonly Color LockedFill = Color.FromHex("#100f0e");
    private static readonly Color LabelPlate = Color.FromHex("#0e0d0c").WithAlpha(0.85f);

    private readonly record struct StyleMetrics(float Col, float Row, float Above, float Below, float Gutter);

    private static readonly StyleMetrics BlueprintMetrics = new(Col: 148, Row: 108, Above: 50, Below: 80, Gutter: 52);
    private static readonly StyleMetrics SchematicMetrics = new(Col: 178, Row: 88, Above: 44, Below: 48, Gutter: 52);

    private StyleMetrics Metrics => Blueprint ? BlueprintMetrics : SchematicMetrics;

    private readonly record struct Look(bool Done, bool Available, bool Locked, bool Blocked,
        bool Shared, bool Mine, bool Active, bool Selected, bool Hovered, float Opacity);

    private Look LookOf(FSResearchNodeView node)
    {
        var matches = _filter.Length == 0 || node.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase);
        return new Look(
            node.IsDone,
            node.State == FSResearchNodeState.Available,
            node.State == FSResearchNodeState.Locked,
            node.State == FSResearchNodeState.ExclusivelyBlocked,
            node.IsActiveResearch && !node.IsDone,
            node.IsMyPersonalPick && !node.IsDone,
            node.IsActive,
            SelectedId == node.Id,
            HoveredId == node.Id,
            matches ? 1f : 0.25f);
    }

    private UIBox2 NodeBounds(FSResearchNodeView node)
        => Blueprint ? BlueprintBounds(node) : SchematicBounds(node);

    private static bool IsFeeding(FSResearchNodeView parent, FSResearchNodeView child)
        => parent.IsDone && child.IsActive;

    private bool OnScreen(Vector2 center, UIBox2 bounds, Vector2 origin, UIBox2 view)
    {
        var box = new UIBox2(origin + (center + bounds.TopLeft) * K, origin + (center + bounds.BottomRight) * K);
        return box.Intersects(view);
    }

    // DrawPrimitives converts its colour to linear but the UI pass never converts back; this cancels that out
    // so primitives match rects and textures drawn with the same palette colour.
    private static Color Prim(Color color) => Color.ToSrgb(color);

    private static Color Fade(Color color, float opacity) => color.WithAlpha(color.A * opacity);

    private static float Pulse(float time, float period) => 0.5f + 0.5f * MathF.Sin(time * MathF.Tau / period);

    private List<string> Wrapped(DrawingHandleScreen handle, FSResearchNodeView node, Font font, float maxWidth)
    {
        if (_wrapFont != font)
        {
            _wrapCache.Clear();
            _wrapFont = font;
        }

        if (!_wrapCache.TryGetValue(node.Id, out var lines))
            _wrapCache[node.Id] = lines = WrapTwoLines(handle, font, node.Name, maxWidth);
        return lines;
    }

    // Breaks at spaces into at most two lines, ending in an ellipsis if the second still overflows.
    private static List<string> WrapTwoLines(DrawingHandleScreen handle, Font font, string text, float maxWidth)
    {
        if (handle.GetDimensions(font, text, 1).X <= maxWidth)
            return new List<string> { text };

        var words = text.Split(' ');
        var first = words[0];
        var i = 1;
        while (i < words.Length && handle.GetDimensions(font, first + " " + words[i], 1).X <= maxWidth)
            first += " " + words[i++];
        if (i >= words.Length)
            return new List<string> { first };

        var second = string.Join(' ', words, i, words.Length - i);
        if (handle.GetDimensions(font, second, 1).X <= maxWidth)
            return new List<string> { first, second };

        while (second.Length > 1 && handle.GetDimensions(font, second + "…", 1).X > maxWidth)
            second = second[..^1].TrimEnd();
        return new List<string> { first, second + "…" };
    }

    // Solid colours throughout: blending happens in linear space, so faint translucent overlays come out far too bright.
    private void DrawGridLines(DrawingHandleScreen handle, Vector2 origin, UIBox2 view, float minor, float major)
    {
        var step = minor * K;
        while (step < 10f)
            step *= 2f;
        var every = Math.Max(1, (int) MathF.Round(major * K / step));

        var first = (int) MathF.Floor((view.Left - origin.X) / step);
        var last = (int) MathF.Ceiling((view.Right - origin.X) / step);
        for (var i = first; i <= last; i++)
        {
            var x = MathF.Round(origin.X + i * step);
            handle.DrawRect(new UIBox2(x, view.Top, x + 1, view.Bottom), i % every == 0 ? GridMajor : GridMinor);
        }

        first = (int) MathF.Floor((view.Top - origin.Y) / step);
        last = (int) MathF.Ceiling((view.Bottom - origin.Y) / step);
        for (var i = first; i <= last; i++)
        {
            var y = MathF.Round(origin.Y + i * step);
            handle.DrawRect(new UIBox2(view.Left, y, view.Right, y + 1), i % every == 0 ? GridMajor : GridMinor);
        }
    }

    private void DrawDots(DrawingHandleScreen handle, Vector2 origin, UIBox2 view, float spacing, Color color)
    {
        handle.DrawRect(view, Paper);
        var step = spacing * K;
        while (step < 10f)
            step *= 2f;
        var size = MathF.Max(1f, MathF.Round(1.5f * K));

        var x0 = (int) MathF.Floor((view.Left - origin.X) / step);
        var x1 = (int) MathF.Ceiling((view.Right - origin.X) / step);
        var y0 = (int) MathF.Floor((view.Top - origin.Y) / step);
        var y1 = (int) MathF.Ceiling((view.Bottom - origin.Y) / step);
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
        {
            var at = new Vector2(MathF.Round(origin.X + x * step), MathF.Round(origin.Y + y * step));
            handle.DrawRect(UIBox2.FromDimensions(at, new Vector2(size)), color);
        }
    }

    private static void DrawSegment(DrawingHandleScreen handle, Vector2 a, Vector2 b, float thickness, Color color)
    {
        var d = b - a;
        var len = d.Length();
        if (len < 0.01f)
            return;
        var n = new Vector2(-d.Y, d.X) / len * (thickness / 2f);
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, new[] { a + n, a - n, b + n, b - n }, Prim(color));
    }

    private static void DrawPolyline(DrawingHandleScreen handle, IReadOnlyList<Vector2> points, float thickness, Color color)
    {
        for (var i = 0; i < points.Count - 1; i++)
            DrawSegment(handle, points[i], points[i + 1], thickness, color);
    }

    // Dashes are indexed along the path rather than accumulated, so float rounding cannot stall the loop.
    private static void DrawDashedPolyline(DrawingHandleScreen handle, IReadOnlyList<Vector2> points, float thickness,
        Color color, float dash, float gap, float phase = 0f)
    {
        var period = dash + gap;
        if (points.Count < 2 || period <= 0.01f)
            return;

        var cumulative = new float[points.Count];
        for (var i = 1; i < points.Count; i++)
            cumulative[i] = cumulative[i - 1] + (points[i] - points[i - 1]).Length();
        var total = cumulative[^1];
        if (total < 0.01f)
            return;

        var offset = (phase % period + period) % period;
        var dashes = (int) MathF.Ceiling((total + offset) / period) + 1;
        for (var k = 0; k < dashes; k++)
        {
            var start = MathF.Max(0f, k * period - offset);
            var end = MathF.Min(total, k * period - offset + dash);
            if (end <= start)
                continue;

            for (var i = 0; i < points.Count - 1; i++)
            {
                var segStart = MathF.Max(start, cumulative[i]);
                var segEnd = MathF.Min(end, cumulative[i + 1]);
                var segLen = cumulative[i + 1] - cumulative[i];
                if (segEnd <= segStart || segLen < 0.01f)
                    continue;
                var dir = (points[i + 1] - points[i]) / segLen;
                DrawSegment(handle, points[i] + dir * (segStart - cumulative[i]), points[i] + dir * (segEnd - cumulative[i]), thickness, color);
            }
        }
    }

    private static List<Vector2> Bezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, int steps = 20)
    {
        var points = new List<Vector2>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float) steps;
            var u = 1 - t;
            points.Add(u * u * u * p0 + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * p3);
        }
        return points;
    }

    private static void DrawArc(DrawingHandleScreen handle, Vector2 center, float radius, float thickness,
        float startAngle, float sweep, Color color)
    {
        var steps = Math.Clamp((int) (MathF.Abs(sweep) / MathF.Tau * radius * 1.5f), 8, 96);
        var verts = new Vector2[(steps + 1) * 2];
        for (var i = 0; i <= steps; i++)
        {
            var angle = startAngle + sweep * i / steps;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            verts[i * 2] = center + dir * (radius + thickness / 2f);
            verts[i * 2 + 1] = center + dir * (radius - thickness / 2f);
        }
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, verts, Prim(color));
    }

    private static void DrawRing(DrawingHandleScreen handle, Vector2 center, float radius, float thickness, Color color)
        => DrawArc(handle, center, radius, thickness, 0f, MathF.Tau, color);

    private static void DrawDashedRing(DrawingHandleScreen handle, Vector2 center, float radius, float thickness,
        float dash, float gap, Color color, float phase = 0f)
    {
        var count = Math.Max(4, (int) MathF.Round(MathF.Tau * radius / (dash + gap)));
        var slot = MathF.Tau / count;
        var arc = slot * dash / (dash + gap);
        for (var i = 0; i < count; i++)
            DrawArc(handle, center, radius, thickness, phase + i * slot, arc, color);
    }

    private static void FillPolygon(DrawingHandleScreen handle, Vector2[] points, Color color)
        => handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, points, Prim(color));

    private static void OutlinePolygon(DrawingHandleScreen handle, Vector2[] points, float thickness, Color color)
    {
        for (var i = 0; i < points.Length; i++)
            DrawSegment(handle, points[i], points[(i + 1) % points.Length], thickness, color);
    }

    private static Vector2[] Hexagon(Vector2 center, float radius)
    {
        var points = new Vector2[6];
        for (var i = 0; i < 6; i++)
        {
            var angle = MathF.PI / 6 + i * MathF.PI / 3;
            points[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
        return points;
    }

    private void DrawDisc(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
        => handle.DrawTextureRect(_discTexture, UIBox2.FromDimensions(center - new Vector2(radius), new Vector2(radius * 2)), color);

    private void DrawGlow(DrawingHandleScreen handle, Vector2 center, float radius, Color color)
        => handle.DrawTextureRect(_glowTexture, UIBox2.FromDimensions(center - new Vector2(radius), new Vector2(radius * 2)), color);

    private static void DrawCheck(DrawingHandleScreen handle, Vector2 center, float size, Color color, float thickness)
    {
        DrawPolyline(handle, new[]
        {
            center + new Vector2(-0.45f, 0.02f) * size,
            center + new Vector2(-0.12f, 0.34f) * size,
            center + new Vector2(0.48f, -0.32f) * size,
        }, thickness, color);
    }

    private static void DrawTextCentered(DrawingHandleScreen handle, Font font, Vector2 center, string text, Color color)
    {
        var dims = handle.GetDimensions(font, text, 1);
        handle.DrawString(font, center - dims / 2f, text, color);
    }

    private float DrawTag(DrawingHandleScreen handle, Font font, Vector2 topLeft, string text, Color fill, Color textColor, Color? border = null)
    {
        var dims = handle.GetDimensions(font, text, 1);
        var pad = new Vector2(5, 1) * K;
        var box = UIBox2.FromDimensions(topLeft, dims + pad * 2);
        handle.DrawRect(box, fill);
        if (border is { } edge)
            handle.DrawRect(box, edge, false);
        handle.DrawString(font, topLeft + pad, text, textColor);
        return box.Width;
    }
}
