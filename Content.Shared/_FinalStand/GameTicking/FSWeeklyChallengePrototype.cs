using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.GameTicking;

[Prototype("fsWeeklyChallenge")]
public sealed partial class FSWeeklyChallengePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField(required: true)]
    public LocId Description { get; private set; }

    [DataField]
    public int Order { get; private set; }

    [DataField]
    public FSDifficulty Difficulty { get; private set; } = FSDifficulty.Normal;

    [DataField]
    public List<ProtoId<FSRoundModifierPrototype>> Modifiers { get; private set; } = new();

    [DataField]
    public int ClearWave { get; private set; } = 20;
}

public static class FSWeeklyRules
{
    public const int ClearPerkPoints = 2;

    private const int LastCallHourUtc = 18;

    private static readonly DateTime Epoch = new(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static string WeekKey(DateTime utc)
    {
        var thursday = WeekStart(utc).AddDays(3);
        return $"{thursday.Year}-W{(thursday.DayOfYear - 1) / 7 + 1:00}";
    }

    public static int WeekIndex(DateTime utc)
        => (int) ((WeekStart(utc) - Epoch).Ticks / TimeSpan.TicksPerDay / 7);

    public static T? Pick<T>(IReadOnlyList<T> rotation, DateTime utc) where T : class
        => rotation.Count == 0 ? null : rotation[(WeekIndex(utc) % rotation.Count + rotation.Count) % rotation.Count];

    public static DateTime WeekStart(DateTime utc)
    {
        var date = DateTime.SpecifyKind(utc.Date, DateTimeKind.Utc);
        return date.AddDays(-(((int) date.DayOfWeek + 6) % 7));
    }

    public static bool IsLastCall(DateTime utc)
        => utc.DayOfWeek == DayOfWeek.Sunday && utc.Hour >= LastCallHourUtc;
}
