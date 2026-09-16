// Draws the bottom-left vitals panel: health figure and bar, stamina bar, then status pills.
using Content.Client._FinalStand.Interface;
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudOverlay
{
    public float? HealthRatio;
    public bool HealthInCrit;
    public float? StaminaRatio;
    public int HealthCurrent;
    public int HealthMax;
    public readonly List<(string Text, bool Bad)> StatusPills = new();

    /// <summary>Matches DefaultGameScreen's bottom band lift so the block sits level with the hands.</summary>
    public const float BottomBandLift = 23f;

    // Aligned with the menu chips at TopLeft margin 10, clearing their 18px height and the vote slot.
    private const float TopLeftX = 10f;
    private const float TopLeftY = 34f;
    private const float TopLeftPad = 8f;

    // The split layout owns the top-left corner with its own action column.
    private const float TopLeftYSeparated = 150f;

    private const float VitalsWidth = 240f;
    private const float VitalsPadX = 8f;
    private const float VitalsPadY = 7f;
    private const float HpBarH = 12f;
    private const float StaminaBarH = 8f;
    private const float VitalsRowGap = 6f;
    private const float PillPadX = 5f;
    private const float PillPadY = 2f;
    private const float PillGap = 3f;

    private static readonly Color VitalsBack = FSPalette.PanelBack;
    private static readonly Color VitalsEdge = FSPalette.PanelEdge;
    private static readonly Color VitalsEdgeHot = FSPalette.PanelEdgeHot;
    private static readonly Color BarTrack = FSPalette.BarTrack;
    private static readonly Color HpFill = FSPalette.HealthFill;
    private static readonly Color HpFillLow = FSPalette.HealthLow;
    private static readonly Color StaminaFill = FSPalette.StaminaFill;
    private static readonly Color StaminaFillLow = FSPalette.StaminaLow;
    private static readonly Color PillBack = FSPalette.CellBack;
    private static readonly Color PillBad = FSPalette.HealthLow;
    private static readonly Color PillGood = FSPalette.Ok;
    private static readonly Color VitalsMuted = FSPalette.TextMuted;

    /// <summary>
    /// Rect with the four corner pixels dropped. DrawRect is the only primitive here, so this is
    /// what a 2-3px radius reduces to at HUD scale - enough to read as rounded, no texture needed.
    /// </summary>
    public static void DrawRounded(DrawingHandleScreen screen, UIBox2 box, Color color, float radius = 3f)
    {
        var r = MathF.Min(radius, MathF.Min(box.Width, box.Height) * 0.5f);
        screen.DrawRect(new UIBox2(box.Left + r, box.Top, box.Right - r, box.Bottom), color);
        screen.DrawRect(new UIBox2(box.Left, box.Top + r, box.Left + r, box.Bottom - r), color);
        screen.DrawRect(new UIBox2(box.Right - r, box.Top + r, box.Right, box.Bottom - r), color);
    }

    private static readonly Color PanelShadow = FSPalette.PanelShadow;
    private static readonly Color PanelSheen = FSPalette.PanelSheen;
    private static readonly Color PanelBevel = Color.Black.WithAlpha(0.30f);

    private const float Chamfer = 3f;
    private const float GrainSize = 64f;
    private static Texture? _grain;

    /// <summary>
    /// The house panel. Corners are chamfered rather than rounded and the edge is drawn as four
    /// rules plus four diagonals, so it reads as a stamped metal plate instead of a CSS rectangle.
    /// A lit top edge and a shadowed bottom edge give it thickness; corner brackets mark it as
    /// deliberate hardware rather than a container.
    /// </summary>
    public static void DrawPanel(DrawingHandleScreen screen, UIBox2 box, Color fill, Color edge,
        Color? accent = null)
    {
        DrawChamfered(screen, new UIBox2(box.Left + 2f, box.Top + 2f, box.Right + 2f, box.Bottom + 2f),
            PanelShadow);
        DrawChamfered(screen, box, fill);

        // Thickness: light catches the top lip, the bottom sits in its own shadow. Where a panel
        // has a role, that lip becomes a colour rule instead - which is what tells them apart at a
        // glance without adding another element to read.
        var lip = new UIBox2(box.Left + Chamfer, box.Top, box.Right - Chamfer, box.Top + 1f);
        if (accent is { } a)
        {
            screen.DrawRect(new UIBox2(lip.Left, lip.Top, lip.Right, lip.Top + 2f), a);
        }
        else
        {
            screen.DrawRect(lip, PanelSheen);
        }
        screen.DrawRect(new UIBox2(box.Left + Chamfer, box.Bottom - 1f, box.Right - Chamfer, box.Bottom), PanelBevel);

        DrawChamferedEdge(screen, box, edge);
        DrawGrain(screen, box);
    }

    /// <summary>
    /// A tiled noise wash over the panel body. Flat fill is what reads as unfinished; a few percent
    /// of grain gives the surface a material without adding anything the eye resolves as detail.
    /// Tiled at native size - stretching noise is immediately obvious.
    /// </summary>
    private static void DrawGrain(DrawingHandleScreen screen, UIBox2 box)
    {
        if (_grain is not { } tile)
            return;

        var tint = Color.White.WithAlpha(0.055f);
        for (var gy = box.Top; gy < box.Bottom; gy += GrainSize)
        {
            for (var gx = box.Left; gx < box.Right; gx += GrainSize)
            {
                var w = MathF.Min(GrainSize, box.Right - gx);
                var h = MathF.Min(GrainSize, box.Bottom - gy);
                screen.DrawTextureRectRegion(tile,
                    new UIBox2(gx, gy, gx + w, gy + h),
                    new UIBox2(0, 0, w, h), tint);
            }
        }
    }

    /// <summary>Body of a panel with its four corners cut at 45 degrees.</summary>
    public static void DrawChamfered(DrawingHandleScreen screen, UIBox2 box, Color color)
    {
        var c = MathF.Min(Chamfer, MathF.Min(box.Width, box.Height) * 0.5f);

        screen.DrawRect(new UIBox2(box.Left + c, box.Top, box.Right - c, box.Bottom), color);
        screen.DrawRect(new UIBox2(box.Left, box.Top + c, box.Left + c, box.Bottom - c), color);
        screen.DrawRect(new UIBox2(box.Right - c, box.Top + c, box.Right, box.Bottom - c), color);

        // Staircase the cut corners so the diagonal is filled rather than left as a notch.
        for (var i = 0; i < c; i++)
        {
            var inset = c - i;
            screen.DrawRect(new UIBox2(box.Left + inset, box.Top + i, box.Left + c, box.Top + i + 1f), color);
            screen.DrawRect(new UIBox2(box.Right - c, box.Top + i, box.Right - inset, box.Top + i + 1f), color);
            screen.DrawRect(new UIBox2(box.Left + inset, box.Bottom - i - 1f, box.Left + c, box.Bottom - i), color);
            screen.DrawRect(new UIBox2(box.Right - c, box.Bottom - i - 1f, box.Right - inset, box.Bottom - i), color);
        }
    }

    private static void DrawChamferedEdge(DrawingHandleScreen screen, UIBox2 box, Color edge)
    {
        var c = MathF.Min(Chamfer, MathF.Min(box.Width, box.Height) * 0.5f);

        screen.DrawLine(new Vector2(box.Left + c, box.Top), new Vector2(box.Right - c, box.Top), edge);
        screen.DrawLine(new Vector2(box.Left + c, box.Bottom), new Vector2(box.Right - c, box.Bottom), edge);
        screen.DrawLine(new Vector2(box.Left, box.Top + c), new Vector2(box.Left, box.Bottom - c), edge);
        screen.DrawLine(new Vector2(box.Right, box.Top + c), new Vector2(box.Right, box.Bottom - c), edge);

        screen.DrawLine(new Vector2(box.Left, box.Top + c), new Vector2(box.Left + c, box.Top), edge);
        screen.DrawLine(new Vector2(box.Right - c, box.Top), new Vector2(box.Right, box.Top + c), edge);
        screen.DrawLine(new Vector2(box.Left, box.Bottom - c), new Vector2(box.Left + c, box.Bottom), edge);
        screen.DrawLine(new Vector2(box.Right - c, box.Bottom), new Vector2(box.Right, box.Bottom - c), edge);
    }

    /// <summary>Text with a hard offset shadow, for labels that sit on the world with no panel.</summary>
    public static void DrawShadowed(DrawingHandleScreen screen, Font font, Vector2 pos, string text, Color color)
    {
        screen.DrawString(font, pos + new Vector2(1f, 1f), text, new Color(0f, 0f, 0f, 0.85f));
        screen.DrawString(font, pos, text, color);
    }

    private float _pillRowH = 17f;
    private int _pillRows;

    /// <summary>
    /// Constant height, whether or not stamina and pills are present. The alert strip above is a
    /// control anchored at build time and cannot track a changing height, so a panel that grew and
    /// shrank would leave a gap that opened and closed as you bled.
    /// </summary>
    public float VitalsBlockHeight()
    {
        if (HealthRatio is null)
            return 0f;

        var h = VitalsPadY * 2f
                + _cachedValueH + 4f + HpBarH
                + VitalsRowGap + StaminaBarH;

        if (_pillRows > 0)
            h += VitalsRowGap + _pillRows * _pillRowH + (_pillRows - 1) * PillGap;

        return h;
    }

    /// <summary>
    /// How many wrapped rows the pills need. Measured before the panel is sized, so the panel and
    /// the alert column above it agree within the same frame.
    /// </summary>
    private int MeasurePillRows(DrawingHandleScreen screen, float innerW)
    {
        if (StatusPills.Count == 0)
            return 0;

        var rows = 1;
        var used = 0f;

        foreach (var (text, _) in StatusPills)
        {
            var w = screen.GetDimensions(_labelFont!, text, 1f).X + PillPadX * 2f;

            if (used > 0f && used + PillGap + w > innerW)
            {
                rows++;
                used = w;
                continue;
            }

            used += (used > 0f ? PillGap : 0f) + w;
        }

        return rows;
    }

    private void DrawVitals(DrawingHandleScreen screen, float margin, float bandLift)
    {
        if (HealthRatio is not { } health)
            return;

        _pillRowH = _cachedLabelH + PillPadY * 2f;
        _pillRows = MeasurePillRows(screen, VitalsWidth - VitalsPadX * 2f);

        var panelH = VitalsBlockHeight();
        var x = margin;
        var bottom = _clyde.ScreenSize.Y - bandLift;
        var top = bottom - panelH;

        var lowHp = health <= 0.3f || HealthInCrit;

        var panelBox = new UIBox2(x, top, x + VitalsWidth, bottom);
        DrawPanel(screen, panelBox, VitalsBack, lowHp ? VitalsEdgeHot : VitalsEdge,
            lowHp ? FSPalette.HealthLow : FSPalette.HealthFill);

        var innerX = x + VitalsPadX;
        var innerW = VitalsWidth - VitalsPadX * 2f;
        var y = top + VitalsPadY;

        // Figure then bar on one baseline, so the number is what the eye lands on first.
        var hpText = HealthCurrent.ToString();
        var hpDims = screen.GetDimensions(_valueFont!, hpText, 1f);
        var rowH = MathF.Max(hpDims.Y, HpBarH);
        // The bar carries "health is red"; the figure stays neutral until something is wrong, so
        // going red actually means something rather than being the resting state.
        screen.DrawString(_valueFont!, new Vector2(innerX, y + (rowH - hpDims.Y) * 0.5f), hpText,
            lowHp ? HpFillLow : FSPalette.TextBright);

        const string healthWord = "HEALTH";
        var wordX = innerX + hpDims.X + 7f;
        screen.DrawString(_labelFont!, new Vector2(wordX, y + (rowH - _cachedLabelH) * 0.5f),
            healthWord, VitalsMuted);

        y += rowH + 4f;

        DrawBar(screen, innerX, y, innerW, HpBarH, health, lowHp ? HpFillLow : HpFill);
        y += HpBarH;

        if (StaminaRatio is { } stamina)
        {
            y += VitalsRowGap;
            DrawBar(screen, innerX, y, innerW, StaminaBarH, stamina,
                stamina <= 0.35f ? StaminaFillLow : StaminaFill);
            y += StaminaBarH;
        }

        if (_pillRows > 0)
            DrawPills(screen, innerX, y + VitalsRowGap, innerW);
    }

    /// <summary>Pills below the bars, wrapping onto further rows. Bleeding and broken bones are
    /// open-ended, so this must wrap rather than truncate.</summary>
    private void DrawPills(DrawingHandleScreen screen, float left, float top, float innerW)
    {
        var px = left;
        var py = top;

        foreach (var (text, bad) in StatusPills)
        {
            var w = screen.GetDimensions(_labelFont!, text, 1f).X + PillPadX * 2f;

            if (px > left && px + w > left + innerW)
            {
                px = left;
                py += _pillRowH + PillGap;
            }

            var color = bad ? PillBad : PillGood;
            var box = new UIBox2(px, py, px + w, py + _pillRowH);
            DrawRounded(screen, box, PillBack, 2f);
            screen.DrawRect(box, color.WithAlpha(0.45f), filled: false);
            screen.DrawString(_labelFont!, new Vector2(px + PillPadX, py + PillPadY), text, color);

            px += w + PillGap;
        }
    }

    private static void DrawBar(DrawingHandleScreen screen, float x, float y, float w, float h, float ratio, Color fill)
    {
        screen.DrawRect(new UIBox2(x, y, x + w, y + h), BarTrack);

        var filled = MathF.Round(w * Math.Clamp(ratio, 0f, 1f));
        if (filled > 0f)
        {
            screen.DrawRect(new UIBox2(x, y, x + filled, y + h), fill);
            // Brighter top third: a flat block of colour reads as a placeholder, not a gauge.
            screen.DrawRect(new UIBox2(x, y, x + filled, y + h * 0.35f), FSPalette.BarSheen);
        }

        screen.DrawRect(new UIBox2(x, y, x + w, y + h), VitalsEdge, filled: false);
    }
}
