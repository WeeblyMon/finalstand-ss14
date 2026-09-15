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
    private const float TopLeftY = 54f;

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

    private float _pillRowH;

    /// <summary>Total height, so anything stacking above this corner can clear it.</summary>
    public float VitalsBlockHeight()
    {
        if (HealthRatio is null)
            return 0f;

        var h = VitalsPadY * 2f + MathF.Max(_cachedValueH, HpBarH);
        if (StaminaRatio is not null)
            h += VitalsRowGap + StaminaBarH;
        if (StatusPills.Count > 0)
            h += VitalsRowGap + _pillRowH;
        return h;
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

        screen.DrawRect(new UIBox2(x, top, x + VitalsWidth, bottom), VitalsBack);
        screen.DrawRect(new UIBox2(x, top, x + VitalsWidth, bottom),
            lowHp ? VitalsEdgeHot : VitalsEdge, filled: false);

        var innerX = x + VitalsPadX;
        var innerW = VitalsWidth - VitalsPadX * 2f;
        var y = top + VitalsPadY;

        // Figure then bar on one baseline, so the number is what the eye lands on first.
        var hpText = HealthCurrent.ToString();
        var hpDims = screen.GetDimensions(_valueFont!, hpText, 1f);
        var rowH = MathF.Max(hpDims.Y, HpBarH);
        screen.DrawString(_valueFont!, new Vector2(innerX, y + (rowH - hpDims.Y) * 0.5f), hpText,
            lowHp ? HpFillLow : HpFill);

        var barX = innerX + hpDims.X + 9f;
        var barW = innerX + innerW - barX;
        DrawBar(screen, barX, y + (rowH - HpBarH) * 0.5f, barW, HpBarH, health,
            lowHp ? HpFillLow : HpFill);
        y += rowH;

        if (StaminaRatio is { } stamina)
        {
            y += VitalsRowGap;
            DrawBar(screen, innerX, y, innerW, StaminaBarH, stamina,
                stamina <= 0.35f ? StaminaFillLow : StaminaFill);
            y += StaminaBarH;
        }

        if (StatusPills.Count == 0)
            return;

        y += VitalsRowGap;
        var px = innerX;
        foreach (var (text, bad) in StatusPills)
        {
            var w = screen.GetDimensions(_labelFont!, text, 1f).X + PillPadX * 2f;
            if (px + w > innerX + innerW && px > innerX)
                break;

            var color = bad ? PillBad : PillGood;
            screen.DrawRect(new UIBox2(px, y, px + w, y + _pillRowH), PillBack);
            screen.DrawRect(new UIBox2(px, y, px + w, y + _pillRowH), color.WithAlpha(0.45f), filled: false);
            screen.DrawString(_labelFont!, new Vector2(px + PillPadX, y + PillPadY), text, color);
            px += w + PillGap;
        }
    }

    private static void DrawBar(DrawingHandleScreen screen, float x, float y, float w, float h, float ratio, Color fill)
    {
        screen.DrawRect(new UIBox2(x, y, x + w, y + h), BarTrack);

        var filled = MathF.Round(w * Math.Clamp(ratio, 0f, 1f));
        if (filled > 0f)
            screen.DrawRect(new UIBox2(x, y, x + filled, y + h), fill);

        screen.DrawRect(new UIBox2(x, y, x + w, y + h), VitalsEdge, filled: false);
    }
}
