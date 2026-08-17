using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Stylesheets;

// The one token set for every FS menu. Windows reference these, never hex literals.
public static class FSUiPalette
{
    public static readonly Color BgDeep = Color.FromHex("#0F172A");
    public static readonly Color BgSurface = Color.FromHex("#151A27");
    public static readonly Color BgElevated = Color.FromHex("#1E2536");
    public static readonly Color BgRecess = Color.FromHex("#0B0E17");
    public static readonly Color BgPressed = Color.FromHex("#181B2B");
    public static readonly Color BgDisabled = Color.FromHex("#0F1018");

    // Unfilled portion of a progress bar or pip row.
    public static readonly Color BgTrack = Color.FromHex("#2A3040");

    public static readonly Color BorderNeutral = Color.FromHex("#475569");
    public static readonly Color BorderSubtle = Color.FromHex("#647089");
    public static readonly Color BorderDisabled = Color.FromHex("#1A1D28");

    public static readonly Color TextPrimary = Color.FromHex("#F8FAFC");
    public static readonly Color TextMuted = Color.FromHex("#8B98AC");

    // Selection ring / primary CTA - nothing else may use this color
    public static readonly Color AccentBrand = Color.FromHex("#818CF8");
    public static readonly Color AccentBrandDim = Color.FromHex("#6B74D6");

    // Semantic - meaning-carrying, never repurposed as decoration
    public static readonly Color StatePositive = Color.FromHex("#22C55E");
    public static readonly Color StateNegative = Color.FromHex("#EF4444");
    public static readonly Color StatePending = Color.FromHex("#FBBF24");
    public static readonly Color StateResearch = Color.FromHex("#A855F7");

    // Money. Separate from StatePending so a price never reads as a warning.
    public static readonly Color Currency = Color.FromHex("#F0B429");

    public const float DisabledOpacity = 0.6f;
    public const int CardCornerRadius = 10;
    public const int PanelCornerRadius = 18;
}
