using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Materials;

public static class FSMaterialText
{
    public static string List(IPrototypeManager proto, IReadOnlyDictionary<ProtoId<StackPrototype>, int> materials)
    {
        var parts = new List<string>();
        foreach (var (material, amount) in materials)
            parts.Add($"{amount} {Loc.GetString(proto.Index(material).Name)}");

        return string.Join(", ", parts);
    }
}
