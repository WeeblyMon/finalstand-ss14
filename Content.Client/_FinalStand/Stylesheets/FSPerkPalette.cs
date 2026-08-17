using System.Collections.Generic;
using Content.Shared._FinalStand.Perks;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Stylesheets;

// Perk category identity. Deliberately its own set: these carry category, not state,
// so they must not be folded into FSUiPalette's semantic tokens.
public static class FSPerkPalette
{
    public static readonly Dictionary<PerkCategory, Color> Background = new()
    {
        [PerkCategory.Red]    = Color.FromHex("#2A1416"),
        [PerkCategory.Blue]   = Color.FromHex("#131E32"),
        [PerkCategory.Green]  = Color.FromHex("#12251C"),
        [PerkCategory.Yellow] = Color.FromHex("#2A2314"),
        [PerkCategory.Purple] = Color.FromHex("#221733"),
    };

    public static readonly Dictionary<PerkCategory, Color> Accent = new()
    {
        [PerkCategory.Red]    = Color.FromHex("#F87171"),
        [PerkCategory.Blue]   = Color.FromHex("#60A5FA"),
        [PerkCategory.Green]  = Color.FromHex("#4ADE80"),
        [PerkCategory.Yellow] = Color.FromHex("#FBBF24"),
        [PerkCategory.Purple] = Color.FromHex("#C084FC"),
    };

    public static readonly Dictionary<PerkCategory, Color[]> LevelRamp = new()
    {
        [PerkCategory.Red]    = [Color.Transparent, Color.FromHex("#4C1D1D"), Color.FromHex("#7F2D2D"), Color.FromHex("#B91C1C"), Color.FromHex("#EF4444")],
        [PerkCategory.Blue]   = [Color.Transparent, Color.FromHex("#1E3A5F"), Color.FromHex("#1D4ED8"), Color.FromHex("#2563EB"), Color.FromHex("#3B82F6")],
        [PerkCategory.Green]  = [Color.Transparent, Color.FromHex("#14532D"), Color.FromHex("#166534"), Color.FromHex("#15803D"), Color.FromHex("#22C55E")],
        [PerkCategory.Yellow] = [Color.Transparent, Color.FromHex("#4A3A0F"), Color.FromHex("#854D0E"), Color.FromHex("#B45309"), Color.FromHex("#FBBF24")],
        [PerkCategory.Purple] = [Color.Transparent, Color.FromHex("#3B1F5C"), Color.FromHex("#6B21A8"), Color.FromHex("#7E22CE"), Color.FromHex("#A855F7")],
    };
}
