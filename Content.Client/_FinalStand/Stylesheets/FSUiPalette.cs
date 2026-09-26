using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Stylesheets;

// The one token set for every FS menu, matched to the HUD's FSPalette. Windows reference these, never hex literals.
public static class FSUiPalette
{
    public static readonly Color BgDeep = Color.FromHex("#121010");
    public static readonly Color BgSurface = Color.FromHex("#181615");
    public static readonly Color BgElevated = Color.FromHex("#201e1c");
    public static readonly Color BgRecess = Color.FromHex("#0b0a0a");
    public static readonly Color BgPressed = Color.FromHex("#42362b");
    public static readonly Color BgDisabled = Color.FromHex("#0f0e0d");

    // Unfilled portion of a progress bar or pip row.
    public static readonly Color BgTrack = Color.FromHex("#2a2826");

    public static readonly Color BorderNeutral = Color.FromHex("#3a3734");
    public static readonly Color BorderSubtle = Color.FromHex("#4c4946");
    public static readonly Color BorderPressed = Color.FromHex("#d2a44e");
    public static readonly Color BorderDisabled = Color.FromHex("#1f1d1b");

    public static readonly Color TextPrimary = Color.FromHex("#dfdcd7");
    public static readonly Color TextMuted = Color.FromHex("#96938d");

    // Selection ring and headings - nothing else may use this color
    public static readonly Color AccentBrand = Color.FromHex("#dfdcd7");

    // Semantic - meaning-carrying, never repurposed as decoration
    public static readonly Color StatePositive = Color.FromHex("#7e9464");
    public static readonly Color StateNegative = Color.FromHex("#c4695a");
    public static readonly Color StatePending = Color.FromHex("#c08a3e");
    public static readonly Color StateResearch = Color.FromHex("#a58bbb");

    // Money, and the one primary call to action per window.
    public static readonly Color Currency = Color.FromHex("#d2a44e");

    public const float DisabledOpacity = 0.6f;
}
