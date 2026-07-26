using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Stylesheets;

// Shared token set from the Master UI Stylesheet Blueprint - one brand accent (selection/primary
// action only) plus a semantic set (meaning-carrying, identical across every FS menu) plus neutrals.
// Referenced by both the Research Computer and the Weapon Shop instead of each keeping its own hex literals.
public static class FSUiPalette
{
    public static readonly Color BgDeep = Color.FromHex("#0F172A");
    public static readonly Color BgSurface = Color.FromHex("#151A27");
    public static readonly Color BgRecess = Color.FromHex("#0B0E17");

    public static readonly Color BorderNeutral = Color.FromHex("#475569");

    public static readonly Color TextPrimary = Color.FromHex("#F8FAFC");
    public static readonly Color TextMuted = Color.FromHex("#8B98AC");

    // Selection ring / primary CTA - nothing else may use this color
    public static readonly Color AccentBrand = Color.FromHex("#818CF8");

    // Semantic - meaning-carrying, never repurposed as decoration
    public static readonly Color StatePositive = Color.FromHex("#22C55E");
    public static readonly Color StateNegative = Color.FromHex("#EF4444");
    public static readonly Color StatePending = Color.FromHex("#FBBF24");

    public const float DisabledOpacity = 0.6f;
    public const int CardCornerRadius = 10;
    public const int PanelCornerRadius = 18;
}
