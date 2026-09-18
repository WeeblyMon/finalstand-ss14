// Draws the medical triage panel on the right edge. Always on for medical staff, hidden for everyone else.
using Content.Client._FinalStand.Interface;
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudOverlay
{
    public readonly record struct TriageRow(string Name, string State, string Range, bool Critical,
        bool Responding, Vector2? Direction);

    public readonly List<TriageRow> TriageRows = new();

    /// <summary>Medical staff keep the panel on screen with nobody down, so it reads as a standing
    /// instrument rather than something that appears from nowhere mid-fight.</summary>
    public bool IsMedicalStaff;

    /// <summary>Shared by the wave panel above it, so the right column has one left edge.</summary>
    public const float RightColumnWidth = 206f;

    private const float TriageWidth = RightColumnWidth;
    private const float ArrowRadius = 7f;
    private const float TriagePad = 8f;
    private const float TriageRowGap = 5f;
    private const float PipSize = 17f;

    private static readonly Color TriageBack = FSPalette.PanelBack;
    private static readonly Color TriageEdge = FSPalette.PanelEdge;
    private static readonly Color TriageMuted = FSPalette.TextMuted;
    private static readonly Color TriageName = FSPalette.TextBright;
    private static readonly Color PipDead = FSPalette.Danger;
    private static readonly Color PipCrit = FSPalette.Warn;
    private static readonly Color PipResponding = FSPalette.Ok;

    /// <summary>Draws under the wave panel. Returns the height used, so nothing stacks into it.</summary>
    private float DrawTriage(DrawingHandleScreen screen, float panelX, float top)
    {
        if (!IsMedicalStaff)
            return 0f;

        var labelH = _cachedLabelH;
        var rowH = MathF.Max(PipSize, labelH);

        // An empty board still reserves one row, so the panel holds its shape instead of snapping
        // to a different size the moment the first casualty lands.
        var rowCount = Math.Max(1, TriageRows.Count);
        var panelH = TriagePad * 2f + labelH + 4f + rowCount * rowH
                     + (rowCount - 1) * TriageRowGap;

        var x = panelX + panelW0 - TriageWidth;
        var box = new UIBox2(x, top, x + TriageWidth, top + panelH);
        DrawPanel(screen, box, TriageBack, TriageEdge, FSPalette.Warn);

        var innerX = x + TriagePad;
        var innerRight = x + TriageWidth - TriagePad;
        var y = top + TriagePad;

        screen.DrawString(_labelFont!, new Vector2(innerX, y), "TRIAGE", TriageMuted);
        y += labelH + 4f;

        if (TriageRows.Count == 0)
        {
            screen.DrawString(_labelFont!, new Vector2(innerX, y + (rowH - labelH) * 0.5f),
                "NO CASUALTIES", PipResponding);
            return panelH;
        }

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

            var tail = row.Range;
            var tailW = screen.GetDimensions(_labelFont!, tail, 1f).X;
            screen.DrawString(_labelFont!, new Vector2(innerRight - tailW, textY), tail, TriageMuted);

            var hasBearing = row.Direction is { } d && d.LengthSquared() > 0f;

            // Bearing arrow. Range alone says how far to run, not which way - this is the compass
            // the old casualty board had, which the panel dropped when it replaced that window.
            if (hasBearing)
            {
                DrawBearing(screen,
                    new Vector2(innerRight - tailW - 8f - ArrowRadius, y + rowH * 0.5f),
                    row.Direction!.Value, pipColor);
            }

            // Names run long and the column is narrow, so the name is clipped to whatever is left
            // after the range and arrow have taken their space rather than drawn straight over them.
            var nameLeft = innerX + PipSize + 6f;
            var nameRight = innerRight - tailW - (hasBearing ? 8f + ArrowRadius * 2f : 0f) - 6f;
            screen.DrawString(_labelFont!, new Vector2(nameLeft, textY),
                Fit(screen, _labelFont!, row.Name, nameRight - nameLeft), TriageName);

            y += rowH + TriageRowGap;
        }

        return panelH;
    }

    /// <summary>Trims text with an ellipsis until it fits, so a long name cannot overrun its row.</summary>
    private static string Fit(DrawingHandleScreen screen, Font font, string text, float maxWidth)
    {
        if (maxWidth <= 0f)
            return string.Empty;

        if (screen.GetDimensions(font, text, 1f).X <= maxWidth)
            return text;

        var trimmed = text;
        while (trimmed.Length > 0 && screen.GetDimensions(font, trimmed + "...", 1f).X > maxWidth)
            trimmed = trimmed[..^1];

        return trimmed.Length == 0 ? string.Empty : trimmed + "...";
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
