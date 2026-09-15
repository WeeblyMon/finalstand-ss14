// The one place HUD colour is defined. Everything else references these.
//
// Direction: the station is dark and amber-lit, so the HUD reads as painted equipment rather than
// as light. Neutrals are warm (R >= G > B) to belong to that world instead of sitting on top of it;
// bone-white carries the primary voice; signal colours are deliberately low-chroma so none of them
// can be mistaken for a lamp. There is no cyan and no saturated green - a glowing accent would
// compete with the map's own lighting for the player's attention.
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Interface;

public static class FSPalette
{
    // Surfaces
    public static readonly Color PanelBack = Color.FromHex("#14100d").WithAlpha(0.84f);
    public static readonly Color PanelDeep = Color.FromHex("#0d0a08").WithAlpha(0.88f);
    public static readonly Color PanelEdge = Color.FromHex("#6e6253").WithAlpha(0.24f);
    public static readonly Color PanelEdgeHot = Color.FromHex("#b3402f").WithAlpha(0.50f);
    public static readonly Color PanelShadow = Color.Black.WithAlpha(0.38f);
    public static readonly Color PanelSheen = Color.White.WithAlpha(0.05f);

    // Type
    public static readonly Color TextBright = Color.FromHex("#e3dccf");
    public static readonly Color TextMuted = Color.FromHex("#9b9284");
    public static readonly Color TextDim = Color.FromHex("#6f6659");

    // Signals. Low chroma on purpose: these mark state, they do not glow.
    public static readonly Color Danger = Color.FromHex("#b3402f");
    public static readonly Color DangerSoft = Color.FromHex("#c4695a");
    public static readonly Color Warn = Color.FromHex("#c08a3e");
    public static readonly Color Ok = Color.FromHex("#7e9464");
    public static readonly Color Money = Color.FromHex("#d2a44e");

    // Gauges
    public static readonly Color BarTrack = Color.White.WithAlpha(0.06f);
    public static readonly Color BarSheen = Color.White.WithAlpha(0.12f);
    // The bars are the one place strong colour is earned - they are the primary readout, and a
    // bone-coloured health bar was under-reading next to everything else.
    public static readonly Color HealthFill = Color.FromHex("#b5453a");
    public static readonly Color HealthLow = Color.FromHex("#e4512f");
    public static readonly Color StaminaFill = Color.FromHex("#4a7fa5");
    public static readonly Color StaminaLow = Color.FromHex("#c08a3e");

    // Interactive
    public static readonly Color ButtonHoverBack = Color.FromHex("#241d17").WithAlpha(0.92f);
    public static readonly Color ButtonHoverEdge = Color.FromHex("#8b7c69").WithAlpha(0.48f);
    public static readonly Color ButtonPressBack = Color.FromHex("#4a3520").WithAlpha(0.92f);
    public static readonly Color ButtonPressEdge = Color.FromHex("#d2a44e").WithAlpha(0.65f);

    // Menu chips sit on the bare world, usually over unlit black, so they need more presence than
    // a panel that has its own backdrop. Panel colours made them vanish up there.
    public static readonly Color ChipBack = Color.FromHex("#2a221a").WithAlpha(0.92f);
    public static readonly Color ChipEdge = Color.FromHex("#8b7c69").WithAlpha(0.45f);

    // Cell backgrounds inside panels (perk sockets, throwable cell, pills).
    public static readonly Color CellBack = Color.FromHex("#1b1611").WithAlpha(0.80f);
    public static readonly Color CellEdge = Color.FromHex("#6e6253").WithAlpha(0.18f);
}
