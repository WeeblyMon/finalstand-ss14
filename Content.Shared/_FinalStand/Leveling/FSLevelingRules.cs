namespace Content.Shared._FinalStand.Leveling;

public static class FSLevelingRules
{
    public const int MilestoneInterval = 5;
    public const int MilestonePerkPoints = 5;
    public const int PerkPointsPerLevel = 1;

    public static int PerkPointsForReaching(int level)
        => level % MilestoneInterval == 0 ? MilestonePerkPoints : PerkPointsPerLevel;
}
