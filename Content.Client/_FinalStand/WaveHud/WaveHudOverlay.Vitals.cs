// Draws the bottom-left vitals block: health over stamina.
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudOverlay
{
    public float? HealthRatio;
    public bool HealthInCrit;
    public float? StaminaRatio;

    private const float VitalsWidth = 168f;
    private const float VitalsHealthH = 18f;
    private const float VitalsStaminaH = 8f;
    private const float VitalsGap = 4f;

    /// <summary>Matches DefaultGameScreen's bottom band lift so the block sits level with the hands.</summary>
    public const float BottomBandLift = 23f;

    // Aligned with the menu chips at TopLeft margin 10, clearing their 18px height and the vote slot.
    private const float TopLeftX = 10f;
    private const float TopLeftY = 54f;

    /// <summary>Height the block occupies, so the alert stack above it can be offset.</summary>
    public const float VitalsHeight = VitalsHealthH + VitalsGap + VitalsStaminaH;

    private static readonly Color VitalsBack = new(0.08f, 0.09f, 0.11f, 0.78f);
    private static readonly Color VitalsEdge = new(0.23f, 0.26f, 0.32f, 0.85f);
    private static readonly Color HealthGood = Color.FromHex("#4FB06A");
    private static readonly Color HealthHurt = Color.FromHex("#D9A441");
    private static readonly Color HealthCrit = Color.FromHex("#E85055");
    private static readonly Color StaminaFull = Color.FromHex("#5B8FC7");
    private static readonly Color StaminaLow = Color.FromHex("#C7A15B");

    private void DrawVitals(DrawingHandleScreen screen, float margin, float bandLift)
    {
        if (HealthRatio is not { } health)
            return;

        var x = margin;
        var bottom = _clyde.ScreenSize.Y - bandLift;
        var top = bottom - VitalsHeight;

        var healthColor = HealthInCrit
            ? HealthCrit
            : health <= 0.3f ? HealthCrit : health <= 0.6f ? HealthHurt : HealthGood;

        DrawBar(screen, x, top, VitalsWidth, VitalsHealthH, health, healthColor);

        if (StaminaRatio is { } stamina)
        {
            DrawBar(screen, x, top + VitalsHealthH + VitalsGap, VitalsWidth, VitalsStaminaH, stamina,
                stamina <= 0.35f ? StaminaLow : StaminaFull);
        }
    }

    private static void DrawBar(DrawingHandleScreen screen, float x, float y, float w, float h, float ratio, Color fill)
    {
        screen.DrawRect(new UIBox2(x, y, x + w, y + h), VitalsBack);

        var filled = MathF.Round(w * Math.Clamp(ratio, 0f, 1f));
        if (filled > 0f)
            screen.DrawRect(new UIBox2(x, y, x + filled, y + h), fill);

        screen.DrawRect(new UIBox2(x, y, x + w, y + h), VitalsEdge, filled: false);
    }
}
