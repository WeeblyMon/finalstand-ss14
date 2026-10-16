using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.GameTicking;

[Serializable, NetSerializable]
public enum FSWaveObjectiveKind : byte
{
    KillQuota,
    NoDowns,
    ClearInTime,
    HoldTheLine,
    Headhunter,
    Fortress,
    HealQuota,
    Bounty,
    SupplyRun,
    NoSpending,
    MeleeKills,
    Untouchable,
}

[Serializable, NetSerializable]
public enum FSWaveObjectiveResult : byte
{
    InProgress,
    Completed,
    Failed,
}

[Prototype("fsWaveObjective")]
public sealed partial class FSWaveObjectivePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField(required: true)]
    public LocId Description { get; private set; }

    [DataField(required: true)]
    public FSWaveObjectiveKind Kind { get; private set; }

    [DataField]
    public int Count { get; private set; }

    [DataField]
    public int CountPerExtraPlayer { get; private set; }

    [DataField]
    public float Seconds { get; private set; }

    [DataField]
    public int Credits { get; private set; } = 300;

    [DataField]
    public int CreditsPerWave { get; private set; } = 25;

    [DataField]
    public int MinWave { get; private set; } = 3;

    [DataField]
    public float Weight { get; private set; } = 1f;

    [DataField]
    public EntProtoId? Spawn { get; private set; }
}

public static class FSWaveObjectiveRules
{
    public static int Target(int count, int perExtraPlayer, int players)
        => count + perExtraPlayer * Math.Max(0, players - 1);

    public static int Reward(int credits, int perWave, int wave, float multiplier)
        => (int) MathF.Round((credits + perWave * Math.Max(0, wave)) * Math.Max(0f, multiplier));

    public static bool Eligible(int minWave, int wave, bool bossWave, bool darkWave)
        => !bossWave && !darkWave && wave >= minWave;

    public static bool CompletesAtWaveEnd(FSWaveObjectiveKind kind) => kind switch
    {
        FSWaveObjectiveKind.NoDowns
            or FSWaveObjectiveKind.ClearInTime
            or FSWaveObjectiveKind.HoldTheLine
            or FSWaveObjectiveKind.Fortress
            or FSWaveObjectiveKind.NoSpending
            or FSWaveObjectiveKind.Untouchable => true,
        _ => false,
    };

    public static int UntouchedNeeded(int players) => (players + 1) / 2;

    public static float PercentLost(float damageTaken, float maxHealth)
        => maxHealth <= 0f ? 0f : Math.Max(0f, damageTaken) / maxHealth * 100f;

    public static T? Pick<T>(IReadOnlyList<T> pool, Func<T, float> weight, float roll) where T : class
    {
        var total = 0f;
        foreach (var item in pool)
            total += Math.Max(0f, weight(item));
        if (total <= 0f)
            return null;

        var target = Math.Clamp(roll, 0f, 0.9999f) * total;
        foreach (var item in pool)
        {
            target -= Math.Max(0f, weight(item));
            if (target < 0f)
                return item;
        }

        return pool[^1];
    }
}

[Serializable, NetSerializable]
public sealed class FSWaveObjectiveStateEvent : EntityEventArgs
{
    public bool Active;
    public string Name = string.Empty;
    public FSWaveObjectiveKind Kind;
    public int Progress;
    public int Target;
    public TimeSpan? EndTime;
    public FSWaveObjectiveResult Result;
    public int Reward;
}
