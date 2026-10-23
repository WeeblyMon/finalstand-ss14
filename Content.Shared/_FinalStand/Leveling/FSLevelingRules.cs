namespace Content.Shared._FinalStand.Leveling;

public static class FSLevelingRules
{
    public static int PerkPointsForReaching(int level) => level % 5 == 0 ? 5 : 1;
}
