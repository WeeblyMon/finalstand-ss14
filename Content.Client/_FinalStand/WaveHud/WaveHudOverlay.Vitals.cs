// Draws the bottom-left vitals panel: health figure and bar, stamina bar, then status pills.
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

    private static readonly Color VitalsBack = new(0.04f, 0.055f, 0.075f, 0.82f);
    private static readonly Color VitalsEdge = new(0.47f, 0.55f, 0.65f, 0.22f);
    private static readonly Color VitalsEdgeHot = new(0.91f, 0.31f, 0.33f, 0.45f);
    private static readonly Color BarTrack = new(1f, 1f, 1f, 0.07f);
    private static readonly Color HpFill = Color.FromHex("#c8d4de");
    private static readonly Color HpFillLow = Color.FromHex("#e85055");
    private static readonly Color StaminaFill = Color.FromHex("#4bb8d8");
    private static readonly Color StaminaFillLow = Color.FromHex("#d9a441");
    private static readonly Color PillBack = Color.FromHex("#0d1218");
    private static readonly Color PillBad = Color.FromHex("#e85055");
    private static readonly Color PillGood = Color.FromHex("#4fbf7a");
    private static readonly Color VitalsMuted = Color.FromHex("#7c8894");

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

    private static readonly Color PanelShadow = new(0f, 0f, 0f, 0.38f);
    private static readonly Color PanelSheen = new(1f, 1f, 1f, 0.05f);

    /// <summary>
    /// The house panel: a dropped shadow, the fill, a one-pixel sheen along the top edge, then the
    /// border. Flat fill plus a hairline border reads as a wireframe; the shadow is what seats the
    /// panel above the world and the sheen is what stops it looking like a hole cut in the screen.
    /// </summary>
    public static void DrawPanel(DrawingHandleScreen screen, UIBox2 box, Color fill, Color edge)
    {
        DrawRounded(screen, new UIBox2(box.Left + 2f, box.Top + 2f, box.Right + 2f, box.Bottom + 2f),
            PanelShadow);
        DrawRounded(screen, box, fill);
        screen.DrawRect(new UIBox2(box.Left + 3f, box.Top, box.Right - 3f, box.Top + 1f), PanelSheen);
        screen.DrawRect(box, edge, filled: false);
    }

    /// <summary>Text with a hard offset shadow, for labels that sit on the world with no panel.</summary>
    public static void DrawShadowed(DrawingHandleScreen screen, Font font, Vector2 pos, string text, Color color)
    {
        screen.DrawString(font, pos + new Vector2(1f, 1f), text, new Color(0f, 0f, 0f, 0.85f));
        screen.DrawString(font, pos, text, color);
    }

    private float _pillRowH = 17f;

    /// <summary>
    /// Constant height, whether or not stamina and pills are present. The alert strip above is a
    /// control anchored at build time and cannot track a changing height, so a panel that grew and
    /// shrank would leave a gap that opened and closed as you bled.
    /// </summary>
    public float VitalsBlockHeight()
    {
        if (HealthRatio is null)
            return 0f;

        return VitalsPadY * 2f
               + MathF.Max(_cachedValueH, _pillRowH) + 4f + HpBarH
               + VitalsRowGap + StaminaBarH;
    }

    private void DrawVitals(DrawingHandleScreen screen, float margin, float bandLift)
    {
        if (HealthRatio is not { } health)
            return;

        _pillRowH = _cachedLabelH + PillPadY * 2f;

        var panelH = VitalsBlockHeight();
        var x = margin;
        var bottom = _clyde.ScreenSize.Y - bandLift;
        var top = bottom - panelH;

        var lowHp = health <= 0.3f || HealthInCrit;

        var panelBox = new UIBox2(x, top, x + VitalsWidth, bottom);
        DrawPanel(screen, panelBox, VitalsBack, lowHp ? VitalsEdgeHot : VitalsEdge);

        var innerX = x + VitalsPadX;
        var innerW = VitalsWidth - VitalsPadX * 2f;
        var y = top + VitalsPadY;

        // Figure then bar on one baseline, so the number is what the eye lands on first.
        var hpText = HealthCurrent.ToString();
        var hpDims = screen.GetDimensions(_valueFont!, hpText, 1f);
        var rowH = MathF.Max(hpDims.Y, HpBarH);
        screen.DrawString(_valueFont!, new Vector2(innerX, y + (rowH - hpDims.Y) * 0.5f), hpText,
            lowHp ? HpFillLow : HpFill);

        const string healthWord = "HEALTH";
        var wordX = innerX + hpDims.X + 7f;
        screen.DrawString(_labelFont!, new Vector2(wordX, y + (rowH - _cachedLabelH) * 0.5f),
            healthWord, VitalsMuted);

        // Pills ride the title row, right-aligned. Giving them their own row meant reserving it
        // even when healthy, which left a band of dead space under the bars.
        DrawPills(screen, innerX + innerW, y + (rowH - _pillRowH) * 0.5f, innerX + hpDims.X + 60f);

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

    }

    /// <summary>Right-aligned run of pills, laid out backwards from <paramref name="right"/>.</summary>
    private void DrawPills(DrawingHandleScreen screen, float right, float y, float leftLimit)
    {
        var px = right;
        foreach (var (text, bad) in StatusPills)
        {
            var w = screen.GetDimensions(_labelFont!, text, 1f).X + PillPadX * 2f;
            if (px - w < leftLimit)
                break;

            px -= w;
            var color = bad ? PillBad : PillGood;
            var box = new UIBox2(px, y, px + w, y + _pillRowH);
            DrawRounded(screen, box, PillBack, 2f);
            screen.DrawRect(box, color.WithAlpha(0.45f), filled: false);
            screen.DrawString(_labelFont!, new Vector2(px + PillPadX, y + PillPadY), text, color);
            px -= PillGap;
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
            screen.DrawRect(new UIBox2(x, y, x + filled, y + h * 0.35f), new Color(1f, 1f, 1f, 0.13f));
        }

        screen.DrawRect(new UIBox2(x, y, x + w, y + h), VitalsEdge, filled: false);
    }
}
