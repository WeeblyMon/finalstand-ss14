using Content.Shared._FinalStand.Perks;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSArchetypeSystem : EntitySystem
{
    private static readonly int CategoryCount = Enum.GetValues<PerkCategory>().Length;

    public FSArchetype Get(EntityUid mindId)
        => TryComp<FSPerkLevelsComponent>(mindId, out var perks) ? Get(perks) : default;

    // Same result as FSArchetypeRules.Resolve, counted without allocating: this runs on every hit a player lands or takes.
    public static FSArchetype Get(FSPerkLevelsComponent perks)
    {
        Span<int> counts = stackalloc int[CategoryCount];
        foreach (var id in perks.Slots)
        {
            if (!string.IsNullOrEmpty(id) && perks.GetLevel(id) > 0 && FSPerkDef.All.TryGetValue(id, out var def))
                counts[(int) def.Category]++;
        }

        // Ties go to the lowest category, as in the shared rule.
        var best = default(FSArchetype);
        for (var i = 0; i < counts.Length; i++)
        {
            if (counts[i] > best.Count)
                best = new FSArchetype((PerkCategory) i, FSArchetypeRules.TierFor(counts[i]), counts[i]);
        }

        return best;
    }

    public FSArchetypeBonus Bonus(EntityUid mindId) => FSArchetypeRules.Bonus(Get(mindId));

    public static FSArchetypeBonus Bonus(FSPerkLevelsComponent perks) => FSArchetypeRules.Bonus(Get(perks));
}
