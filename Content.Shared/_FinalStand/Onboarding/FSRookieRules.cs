using Content.Shared._FinalStand.Titles;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Onboarding;

public static class FSRookieRules
{
    public static readonly TimeSpan RookieThreshold = TimeSpan.FromHours(10);

    public const int StarterPerkPoints = 5;
    public const int FirstRoundPerkPoints = 5;
    public const int FirstRoundMinWaves = 5;
    public const int GraduationPerkPoints = 3;
    public const float CatchUpXpMultiplier = 2f;

    public static readonly ProtoId<FSTitlePrototype> FirstRoundTitle = "FSTitleFreshMeat";
    public static readonly ProtoId<FSTitlePrototype> GraduationTitle = "FSTitleSurvivor";

    public static bool IsRookie(TimeSpan overallPlaytime) => overallPlaytime < RookieThreshold;
}
