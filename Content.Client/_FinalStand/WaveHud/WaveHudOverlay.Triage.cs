// Draws the medical triage panel on the right edge. Renders nothing when nobody is in trouble.
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudOverlay
{
    public readonly record struct TriageRow(string Name, string State, string Range, bool Critical,
        bool Responding, Vector2? Direction);

    public readonly List<TriageRow> TriageRows = new();

    private const float TriageWidth = 262f;
    private const float ArrowRadius = 6f;
    private const float TriagePad = 8f;
    private const float TriageRowGap = 5f;
    private const float PipSize = 17f;

    private static readonly Color TriageBack = new(0.04f, 0.055f, 0.075f, 0.82f);
    private static readonly Color TriageEdge = new(0.47f, 0.55f, 0.65f, 0.22f);
    private static readonly Color TriageMuted = Color.FromHex("#7c8894");
    private static readonly Color TriageName = Color.FromHex("#D8E0E8");
    private static readonly Color PipDead = Color.FromHex("#e85055");
    private static readonly Color PipCrit = Color.FromHex("#d9a441");
    private static readonly Color PipResponding = Color.FromHex("#4fbf7a");

    /// <summary>Draws under the wave panel. Returns the height used, so nothing stacks into it.</summary>
    private float DrawTriage(DrawingHandleScreen screen, float panelX, float top)
    {
        if (TriageRows.Count == 0)
            return 0f;

        var labelH = _cachedLabelH;
        var rowH = MathF.Max(PipSize, labelH);
        var panelH = TriagePad * 2f + labelH + 4f + TriageRows.Count * rowH
                     + (TriageRows.Count - 1) * TriageRowGap;

        var x = panelX + panelW0 - TriageWidth;
        var box = new UIBox2(x, top, x + TriageWidth, top + panelH);
        DrawPanel(screen, box, TriageBack, TriageEdge);

        var innerX = x + TriagePad;
        var innerRight = x + TriageWidth - TriagePad;
        var y = top + TriagePad;

        screen.DrawString(_labelFont!, new Vector2(innerX, y), "TRIAGE", TriageMuted);
        y += labelH + 4f;

        foreach (var row in TriageRows)
        {
            var pipColor = row.Responding ? PipResponding : row.Critical ? PipCrit : PipDead;
            var pip = new UIBox2(innerX, y + (rowH - PipSize) * 0.5f, innerX + PipSize,
                y + (rowH - PipSize) * 0.5f + PipSize);
            DrawRounded(screen, pip, pipColor, 2f);

            var glyph = row.Responding ? "+" : row.Critical ? "!" : "x";
            var glyphDims = screen.GetDimensions(_labelFont!, glyph, 1f);
            screen.DrawString(_labelFont!,
                new Vector2(pip.Left + (PipSize - glyphDims.X) * 0.5f, pip.Top + (PipSize - glyphDims.Y) * 0.5f),
                glyph, Color.Black);

            var textY = y + (rowH - labelH) * 0.5f;
            screen.DrawString(_labelFont!, new Vector2(innerX + PipSize + 6f, textY), row.Name, TriageName);

            var tail = $"{row.State} {row.Range}".Trim();
            var tailW = screen.GetDimensions(_labelFont!, tail, 1f).X;
            screen.DrawString(_labelFont!, new Vector2(innerRight - tailW, textY), tail, TriageMuted);

            // Bearing arrow. Range alone says how far to run, not which way - this is the compass
            // the old casualty board had, which the panel dropped when it replaced that window.
            if (row.Direction is { } dir && dir.LengthSquared() > 0f)
            {
                DrawBearing(screen,
                    new Vector2(innerRight - tailW - 8f - ArrowRadius, y + rowH * 0.5f),
                    dir, pipColor);
            }

            y += rowH + TriageRowGap;
        }

        return panelH;
    }

    /// <summary>A triangle pointed along <paramref name="dir"/>, which is already screen-space.</summary>
    private static void DrawBearing(DrawingHandleScreen screen, Vector2 centre, Vector2 dir, Color color)
    {
        var d = dir.Normalized();
        var perp = new Vector2(-d.Y, d.X);

        var tip = centre + d * ArrowRadius;
        var back = centre - d * (ArrowRadius * 0.55f);
        var left = back + perp * (ArrowRadius * 0.8f);
        var right = back - perp * (ArrowRadius * 0.8f);

        // A plain array, not a collection expression: `Span<Vector2> t = [a, b, c]` compiles to
        // InlineArray3<T>, which the client sandbox rejects at load.
        var tri = new[] { tip, left, right };
        screen.DrawPrimitives(DrawPrimitiveTopology.TriangleList, tri, color);
    }
}
