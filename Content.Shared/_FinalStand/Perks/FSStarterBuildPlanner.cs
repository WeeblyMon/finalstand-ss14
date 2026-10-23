namespace Content.Shared._FinalStand.Perks;

public static class FSStarterBuildPlanner
{
    public readonly record struct Plan(Dictionary<string, int> Levels, int Spent);

    public static Plan Make(IReadOnlyDictionary<string, int> owned, IReadOnlyList<string> buildPerks, int points)
    {
        var levels = new Dictionary<string, int>(owned);
        var spent = 0;

        for (var target = 1; target <= FSPerkDef.MaxLevel; target++)
        {
            foreach (var perk in buildPerks)
            {
                var current = levels.GetValueOrDefault(perk);
                if (current >= target)
                    continue;

                var cost = FSPerkDef.CostForUpgrade(current);
                if (spent + cost > points)
                    return new Plan(levels, spent);

                levels[perk] = current + 1;
                spent += cost;
            }
        }

        return new Plan(levels, spent);
    }

    public static string[] Slots(IReadOnlyDictionary<string, int> levels, IReadOnlyList<string> buildPerks)
    {
        var slots = new string[FSPerkDef.SlotCount];
        Array.Fill(slots, string.Empty);

        var next = 0;
        foreach (var perk in buildPerks)
        {
            if (next >= FSPerkDef.SlotCount)
                break;
            if (levels.GetValueOrDefault(perk) > 0)
                slots[next++] = perk;
        }

        return slots;
    }
}
