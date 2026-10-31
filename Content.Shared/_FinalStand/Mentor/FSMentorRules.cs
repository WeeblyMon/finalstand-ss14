namespace Content.Shared._FinalStand.Mentor;

public static class FSMentorRules
{
    public static readonly TimeSpan MinPlaytime = TimeSpan.FromHours(25);
    public const int MinLevel = 15;

    // Mentor point awards: the server grants them, the mentor window lists them.
    public const int ReviveMp = 3;
    public const float HealPerMp = 50f;
    public const int WaveEscortMp = 1;
    public const int FirstRoundMp = 5;

    public static bool IsEligible(TimeSpan playtime, int level, int prestige)
        => playtime >= MinPlaytime && (level >= MinLevel || prestige > 0);

    public static int HealPoints(ref float bank, float healed, float perPoint)
    {
        bank += Math.Max(0f, healed);
        var points = (int) (bank / perPoint);
        bank -= points * perPoint;
        return points;
    }
}
