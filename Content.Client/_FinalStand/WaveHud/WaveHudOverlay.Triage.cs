// Draws the medical triage panel on the right edge.
using Content.Client._FinalStand.Ailments;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Ailments;
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudOverlay
{
    public readonly record struct TriageRow(string Name, string State, Color StateColor, string Range,
        string? Responder, Vector2? Direction, FSAilmentPips Pips, string Needs);

    public readonly List<TriageRow> TriageRows = new();

    public bool IsMedicalStaff;

    public const float RightColumnWidth = 206f;

    private const float TriageWidth = RightColumnWidth;
    private const float ArrowRadius = 7f;
    private const float TriagePad = 8f;
    private const float TriageRowGap = 5f;
    private const float TriageChipPad = 4f;
    private const float TriagePipSize = 9f;

    private static readonly Color TriageBack = FSPalette.PanelBack;
    private static readonly Color TriageEdge = FSPalette.PanelEdge;
    private static readonly Color TriageMuted = FSPalette.TextMuted;
    private static readonly Color TriageName = FSPalette.TextBright;
    private static readonly Color TriageResponding = FSPalette.Ok;

    private FSAilmentVisualsSystem? _ailmentVisuals;

    private static bool HasDetail(TriageRow row)
    {
        return row.Pips != FSAilmentPips.None || row.Needs.Length > 0 || row.Responder != null;
    }

    private float TriageRowHeight(TriageRow row, float lineH)
    {
        return HasDetail(row) ? lineH * 2f + 2f : lineH;
    }

    private float DrawTriage(DrawingHandleScreen screen, float panelX, float top)
    {
        if (!IsMedicalStaff)
            return 0f;

        _ailmentVisuals ??= _entityManager.System<FSAilmentVisualsSystem>();

        var labelH = _cachedLabelH;
        var lineH = labelH + 2f;

        var rowsH = 0f;
        foreach (var row in TriageRows)
        {
            rowsH += TriageRowHeight(row, lineH);
        }

        var rowCount = Math.Max(1, TriageRows.Count);
        var panelH = TriagePad * 2f + labelH + 4f + (TriageRows.Count == 0 ? lineH : rowsH)
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
            screen.DrawString(_labelFont!, new Vector2(innerX, y + 1f), "NO CASUALTIES", TriageResponding);
            return panelH;
        }

        foreach (var row in TriageRows)
        {
            var textY = y + 1f;

            var chipW = screen.GetDimensions(_labelFont!, row.State, 1f).X + TriageChipPad * 2f;
            var chip = new UIBox2(innerX, y, innerX + chipW, y + lineH);
            DrawRounded(screen, chip, FSPalette.CellBack, 2f);
            screen.DrawRect(chip, row.StateColor.WithAlpha(0.6f), filled: false);
            screen.DrawString(_labelFont!, new Vector2(innerX + TriageChipPad, textY), row.State, row.StateColor);

            var tail = row.Range;
            var tailW = screen.GetDimensions(_labelFont!, tail, 1f).X;
            screen.DrawString(_labelFont!, new Vector2(innerRight - tailW, textY), tail, TriageMuted);

            var hasBearing = row.Direction is { } d && d.LengthSquared() > 0f;
            if (hasBearing)
            {
                FSBearingArrow.DrawArrow(screen,
                    new Vector2(innerRight - tailW - 8f - ArrowRadius, y + lineH * 0.5f),
                    row.Direction!.Value.Normalized(), ArrowRadius, row.StateColor);
            }

            var nameLeft = chip.Right + 5f;
            var nameRight = innerRight - tailW - (hasBearing ? 8f + ArrowRadius * 2f : 0f) - 4f;
            screen.DrawString(_labelFont!, new Vector2(nameLeft, textY),
                Fit(screen, _labelFont!, row.Name, nameRight - nameLeft), TriageName);

            if (HasDetail(row))
            {
                var detailY = y + lineH + 2f;
                var detailX = innerX;
                detailX += _ailmentVisuals.DrawScreenPips(screen,
                    new Vector2(detailX, detailY + (lineH - TriagePipSize) * 0.5f), row.Pips, TriagePipSize);

                if (row.Needs.Length > 0)
                {
                    if (detailX > innerX)
                        detailX += 4f;

                    screen.DrawString(_labelFont!, new Vector2(detailX, detailY + 1f), row.Needs, FSPalette.Money);
                    detailX += screen.GetDimensions(_labelFont!, row.Needs, 1f).X;
                }

                if (row.Responder is { } responder)
                {
                    var tag = Fit(screen, _labelFont!, "> " + responder, innerRight - detailX - 6f);
                    var tagW = screen.GetDimensions(_labelFont!, tag, 1f).X;
                    screen.DrawString(_labelFont!, new Vector2(innerRight - tagW, detailY + 1f), tag, TriageResponding);
                }
            }

            y += TriageRowHeight(row, lineH) + TriageRowGap;
        }

        return panelH;
    }

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
}
