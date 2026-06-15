using Robust.Shared.Serialization;

namespace Content.Shared.Humanoid.Markings;

[DataDefinition, Serializable, NetSerializable]
public sealed partial class MarkingSet
{
    [DataField]
    public Dictionary<MarkingCategories, List<Marking>> Markings { get; set; } = new();

    public bool TryGetCategory(MarkingCategories category, out List<Marking> markings)
        => Markings.TryGetValue(category, out markings!);

    public void AddMarking(MarkingCategories category, Marking marking)
    {
        if (!Markings.TryGetValue(category, out var list))
        {
            list = new();
            Markings[category] = list;
        }
        list.Add(marking);
    }

    public void RemoveMarking(MarkingCategories category, string markingId)
    {
        if (!Markings.TryGetValue(category, out var list))
            return;
        list.RemoveAll(m => m.MarkingId == markingId);
    }
}
