using System.Collections.Generic;
using Content.Shared._FinalStand.Perks;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Stylesheets;

// Perk category identity, toned to the HUD. These carry category, not state,
// so they must not be folded into FSUiPalette's semantic tokens.
public static class FSPerkPalette
{
    public static readonly Dictionary<PerkCategory, Color> Background = new()
    {
        [PerkCategory.Red]    = Color.FromHex("#2a1a17"),
        [PerkCategory.Blue]   = Color.FromHex("#17202a"),
        [PerkCategory.Green]  = Color.FromHex("#1c2218"),
        [PerkCategory.Yellow] = Color.FromHex("#29221a"),
        [PerkCategory.Purple] = Color.FromHex("#221c27"),
    };

    public static readonly Dictionary<PerkCategory, Color> Accent = new()
    {
        [PerkCategory.Red]    = Color.FromHex("#c4695a"),
        [PerkCategory.Blue]   = Color.FromHex("#6f9dc0"),
        [PerkCategory.Green]  = Color.FromHex("#9bb07f"),
        [PerkCategory.Yellow] = Color.FromHex("#c08a3e"),
        [PerkCategory.Purple] = Color.FromHex("#a58bbb"),
    };

    public static readonly Dictionary<PerkCategory, Color> Edge = new()
    {
        [PerkCategory.Red]    = Color.FromHex("#5a2c24"),
        [PerkCategory.Blue]   = Color.FromHex("#2e4a60"),
        [PerkCategory.Green]  = Color.FromHex("#3e4a2f"),
        [PerkCategory.Yellow] = Color.FromHex("#5c4a26"),
        [PerkCategory.Purple] = Color.FromHex("#4a3b58"),
    };
}
