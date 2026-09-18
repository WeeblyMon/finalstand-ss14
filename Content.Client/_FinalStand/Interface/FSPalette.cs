// The one place HUD colour is defined.
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Interface;

public static class FSPalette
{
    public static readonly Color PanelBack = Color.FromHex("#121010").WithAlpha(0.84f);
    public static readonly Color PanelDeep = Color.FromHex("#0b0a0a").WithAlpha(0.88f);
    public static readonly Color PanelEdge = Color.FromHex("#67635e").WithAlpha(0.24f);
    public static readonly Color PanelEdgeHot = Color.FromHex("#b3402f").WithAlpha(0.50f);
    public static readonly Color PanelShadow = Color.Black.WithAlpha(0.38f);
    public static readonly Color PanelSheen = Color.White.WithAlpha(0.05f);

    public static readonly Color TextBright = Color.FromHex("#dfdcd7");
    public static readonly Color TextMuted = Color.FromHex("#96938d");
    public static readonly Color TextDim = Color.FromHex("#6a6761");

    public static readonly Color Danger = Color.FromHex("#b3402f");
    public static readonly Color DangerSoft = Color.FromHex("#c4695a");
    public static readonly Color Warn = Color.FromHex("#c08a3e");
    public static readonly Color Ok = Color.FromHex("#7e9464");
    public static readonly Color Money = Color.FromHex("#d2a44e");

    public static readonly Color BarTrack = Color.White.WithAlpha(0.06f);
    public static readonly Color BarSheen = Color.White.WithAlpha(0.12f);
    public static readonly Color HealthFill = Color.FromHex("#b5453a");
    public static readonly Color HealthLow = Color.FromHex("#e4512f");
    public static readonly Color StaminaFill = Color.FromHex("#4a7fa5");
    public static readonly Color StaminaLow = Color.FromHex("#c08a3e");

    public static readonly Color ButtonHoverBack = Color.FromHex("#201e1c").WithAlpha(0.92f);
    public static readonly Color ButtonHoverEdge = Color.FromHex("#827d77").WithAlpha(0.48f);
    public static readonly Color ButtonPressBack = Color.FromHex("#42362b").WithAlpha(0.92f);
    public static readonly Color ButtonPressEdge = Color.FromHex("#d2a44e").WithAlpha(0.65f);

    public static readonly Color ChipBack = Color.FromHex("#252320").WithAlpha(0.92f);
    public static readonly Color ChipEdge = Color.FromHex("#827d77").WithAlpha(0.45f);

    public static readonly Color CellBack = Color.FromHex("#181615").WithAlpha(0.80f);
    public static readonly Color CellEdge = Color.FromHex("#67635e").WithAlpha(0.18f);
}
