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
        if (recipe.CapGroup is { } group && fs.Caps.TryGetValue(group, out var max))
            count = Math.Min(count, Math.Max(0, max - fs.CapsUsed.GetValueOrDefault(group)));

        var materials = entMan.System<FSCarriedMaterialSystem>();
        foreach (var (material, need) in recipe.Materials)
        {
            var have = user is { } u ? materials.Count(u, material) : 0;
            count = Math.Min(count, need > 0 ? have / need : count);
        }

        return count;
    }

    public static string Info(IEntityManager entMan, IPrototypeManager proto, EntityUid? user,
        FSEngineerOnlyRCDComponent fs, RCDPrototype piece)
    {
        var lines = new List<string> { $"[bold]{FSRCDText.Name(proto, piece)}[/bold]" };
        if (piece.Mode == RcdMode.Deconstruct)
        {
            lines.Add(Loc.GetString("fs-rcd-info-deconstruct"));
            return string.Join("\n", lines);
        }

        var footer = Loc.GetString("fs-rcd-info-time", ("seconds", piece.Delay));
        if (proto.TryIndex<FSBuildRecipePrototype>(piece.ID, out var recipe))
        {
            lines.Add(FSRCDText.Costs(entMan, proto, user, recipe, "\n"));
            if (recipe.CapGroup is { } group)
                footer = $"{FSRCDText.CapShort(fs, group)} · {footer}";
        }

        lines.Add($"[color=#96938d]{footer}[/color]");
        return string.Join("\n", lines);
    }
}
