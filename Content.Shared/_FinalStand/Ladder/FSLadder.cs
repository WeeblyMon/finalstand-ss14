using Content.Shared._FinalStand.GameTicking;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Ladder;

[Serializable, NetSerializable]
public enum FSPlayerBracket : byte
{
    Solo,
    Squad,
    Crew,
}

[Serializable, NetSerializable]
public sealed record FSRunRecord(
    string Players,
    FSDifficulty Difficulty,
    FSPlayerBracket Bracket,
    int Waves,
    bool Victory,
    int DurationSeconds,
    int Kills,
    DateTime EndedAtUtc,
    string? Weekly = null);

public static class FSLadder
{
    public const int PageSize = 10;

    public static FSPlayerBracket BracketFor(int players) => players switch
    {
        <= 1 => FSPlayerBracket.Solo,
        <= 4 => FSPlayerBracket.Squad,
        _ => FSPlayerBracket.Crew,
    };

    public static string BracketLocId(FSPlayerBracket bracket) => bracket switch
    {
        FSPlayerBracket.Squad => "fs-ladder-bracket-squad",
        FSPlayerBracket.Crew => "fs-ladder-bracket-crew",
        _ => "fs-ladder-bracket-solo",
    };

    public static int Compare(FSRunRecord a, FSRunRecord b)
    {
        var byVictory = b.Victory.CompareTo(a.Victory);
        if (byVictory != 0)
            return byVictory;

        var byWaves = b.Waves.CompareTo(a.Waves);
        if (byWaves != 0)
            return byWaves;

        var byTime = a.DurationSeconds.CompareTo(b.DurationSeconds);
        return byTime != 0 ? byTime : a.EndedAtUtc.CompareTo(b.EndedAtUtc);
    }

    public static bool Beats(FSRunRecord challenger, FSRunRecord? holder)
        => holder == null || Compare(challenger, holder) < 0;
}

[Serializable, NetSerializable]
public sealed class FSLadderRequestEvent : EntityEventArgs
{
    public FSDifficulty Difficulty;
    public FSPlayerBracket Bracket;
    public bool Weekly;
}

[Serializable, NetSerializable]
public sealed class FSLadderResponseEvent : EntityEventArgs
{
    public FSDifficulty Difficulty;
    public FSPlayerBracket Bracket;
    public bool Weekly;
    public string? WeeklyName;
    public FSRunRecord[] Runs = Array.Empty<FSRunRecord>();
}

[Serializable, NetSerializable]
public sealed class FSOpenLadderEvent : EntityEventArgs
{
}
