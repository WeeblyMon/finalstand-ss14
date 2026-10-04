using Content.Shared._FinalStand.Materials;
using Content.Shared._FinalStand.RCD;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.RCD;

// The RCD's pieces as both pickers see them: fortifications in palette order, station pieces, and deconstruct.
public sealed class FSRCDPalette
{
    public readonly List<RCDPrototype> Pieces = new();
    public readonly List<RCDPrototype> Station = new();
    public RCDPrototype? Deconstruct;

    public FSRCDPalette(IPrototypeManager proto, RCDComponent rcd, FSEngineerOnlyRCDComponent fs)
    {
        var available = rcd.AvailablePrototypes;
        foreach (var id in fs.Palette)
        {
            if (available.Contains(id))
                Pieces.Add(proto.Index(id));
        }

        foreach (var id in available)
        {
            var piece = proto.Index(id);
            if (piece.Mode == RcdMode.Deconstruct)
                Deconstruct = piece;
            else if (!fs.Palette.Contains(id))
                Station.Add(piece);
        }

        Station.Sort((a, b) => string.CompareOrdinal(a.Category + a.ID, b.Category + b.ID));
    }

    // How many of this piece the user could build right now, counting every material and both caps.
    public static int Affordable(IEntityManager entMan, IPrototypeManager proto, EntityUid? user,
        FSEngineerOnlyRCDComponent fs, RCDPrototype piece)
    {
        if (!proto.TryIndex<FSBuildRecipePrototype>(piece.ID, out var recipe))
            return -1;

        var count = Math.Max(0, fs.StationCap - fs.StationUsed);
        if (recipe.CapGroup is { } group && fs.CapsMax.TryGetValue(group, out var max))
            count = Math.Min(count, Math.Max(0, max - fs.CapsUsed.GetValueOrDefault(group)));

        var materials = entMan.System<FSCarriedMaterialSystem>();
        foreach (var (material, need) in recipe.Materials)
        {
            var have = FSRCDText.Have(materials, user, fs, material);
            count = Math.Min(count, need > 0 ? have / need : count);
        }

        return count;
    }
}
