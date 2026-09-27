namespace Content.Shared._FinalStand.MedicalOps;

public static class FSMedicalBonusText
{
    public static List<string> Describe(Dictionary<FSMedicalBonusCategory, float> bonuses)
    {
        var lines = new List<string>(bonuses.Count);
        foreach (var (category, fraction) in bonuses)
        {
            lines.Add(Loc.GetString("fs-bonus-line",
                ("percent", MathF.Round(fraction * 100f)),
                ("stat", Loc.GetString($"fs-bonus-stat-{category.ToString().ToLowerInvariant()}"))));
        }

        return lines;
    }
}
