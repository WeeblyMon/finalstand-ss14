using Content.Shared._FinalStand.Materials;
using Content.Shared._FinalStand.RCD;
using Content.Shared.RCD;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.RCD;

// Text for the RCD pickers and the HUD weapon panel.
public static class FSRCDText
{
    private const string Enough = "#8fe07a";
    private const string Short = "#ff6b5e";

    // Carried sheets plus whatever the server says the holder can draw from silos in reach.
    public static int Have(FSCarriedMaterialSystem materials, EntityUid? user, FSEngineerOnlyRCDComponent fs, ProtoId<StackPrototype> material)
    {
        return (user is { } u ? materials.Count(u, material) : 0) + fs.SiloStock.GetValueOrDefault(material);
    }

    public static string Name(IPrototypeManager protoMan, RCDPrototype proto)
    {
        var name = proto.Mode is RcdMode.ConstructObject or RcdMode.ConstructTile
                   && proto.Prototype != null
                   && protoMan.TryIndex(proto.Prototype, out EntityPrototype? entProto)
            ? entProto.Name
            : Loc.GetString(proto.SetName);

        return name.Length == 0 ? name : Concat(char.ToUpper(name[0]).ToString(), name.Substring(1));
    }

    // Separate method so Roslyn doesn't fold it into a call the sandbox rejects.
    private static string Concat(string a, string b) => a + b;

    public static string Costs(IEntityManager entMan, IPrototypeManager proto, EntityUid? user, FSEngineerOnlyRCDComponent fs, FSBuildRecipePrototype recipe, string separator = "  ")
    {
        var materials = entMan.System<FSCarriedMaterialSystem>();
        var parts = new List<string>();
        foreach (var (material, need) in recipe.Materials)
        {
            var have = Have(materials, user, fs, material);
            parts.Add($"[color={(have >= need ? Enough : Short)}]{have}/{need}[/color] {Loc.GetString(proto.Index(material).Name)}");
        }

        return string.Join(separator, parts);
    }

    // One plain line for the HUD weapon panel, which can't take markup: what you can build, or what you're short.
    public static string Summary(IEntityManager entMan, IPrototypeManager proto, EntityUid? user,
        FSEngineerOnlyRCDComponent fs, RCDPrototype piece)
    {
        if (piece.Mode == RcdMode.Deconstruct)
            return Loc.GetString("fs-rcd-summary-deconstruct");

        var name = Name(proto, piece);
        if (!proto.TryIndex<FSBuildRecipePrototype>(piece.ID, out var recipe))
            return name;

        var count = FSRCDPalette.Affordable(entMan, proto, user, fs, piece);
        if (count > 0)
            return Loc.GetString("fs-rcd-summary-ready", ("name", name), ("count", count));

        var materials = entMan.System<FSCarriedMaterialSystem>();
        var missing = new List<string>();
        foreach (var (material, need) in recipe.Materials)
        {
            var have = Have(materials, user, fs, material);
            if (have < need)
                missing.Add($"{need - have} {Loc.GetString(proto.Index(material).Name)}");
        }

        return missing.Count > 0
            ? Loc.GetString("fs-rcd-summary-short", ("name", name), ("list", string.Join(", ", missing)))
            : Loc.GetString("fs-rcd-summary-capped", ("name", name));
    }

    public static string CapShort(FSEngineerOnlyRCDComponent comp, string group)
    {
        return $"{Loc.GetString("fs-rcd-cap-group-" + group)} {comp.CapsUsed.GetValueOrDefault(group)}/{comp.Caps.GetValueOrDefault(group)}";
    }
}
