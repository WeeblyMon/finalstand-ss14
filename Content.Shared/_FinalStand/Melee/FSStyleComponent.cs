using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Melee;

// Melee style score behind the D to SSS rank meter. The server adds to it; both sides work out the decay.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSStyleComponent : Component
{
    public override bool SendOnlyToOwner => true;

    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public float Score;

    // When Score was last set; decay counts from here.
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public TimeSpan Updated;

    // Melee hits in the current chain, shown under the meter.
    [ViewVariables, AutoNetworkedField]
    public int Hits;

    [AutoNetworkedField]
    public TimeSpan LastHit;

    public readonly List<string> RecentMoves = new();
    public EntityUid? LastWeapon;
    public TimeSpan LastWeaponTime;
    public TimeSpan LastKill;
    public int KillStreak;
    public TimeSpan LastDrop;

    public static readonly string[] RankNames = ["", "D", "C", "B", "A", "S", "SS", "SSS"];

    // Score needed to reach each rank; index 0 is no rank.
    public static readonly float[] Thresholds = [0f, 40f, 120f, 240f, 400f, 600f, 850f, 1150f];

    // Points lost per second at each rank once the grace runs out; higher ranks drain faster.
    public static readonly float[] Decay = [0f, 12f, 18f, 26f, 36f, 48f, 62f, 80f];

    public const float MaxScore = 1400f;
    public const int MaxRank = 7;
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(1.2);
    public static readonly TimeSpan HitChainWindow = TimeSpan.FromSeconds(2.5);

    public static int RankOf(float score)
    {
        for (var rank = MaxRank; rank > 0; rank--)
        {
            if (score >= Thresholds[rank])
                return rank;
        }

        return 0;
    }

    // How far through the current rank the score is, from 0 to 1.
    public static float Progress(float score)
    {
        var rank = RankOf(score);
        var floor = Thresholds[rank];
        var ceiling = rank == MaxRank ? MaxScore : Thresholds[rank + 1];
        return Math.Clamp((score - floor) / (ceiling - floor), 0f, 1f);
    }

    public float ScoreAt(TimeSpan now)
    {
        var elapsed = (float) (now - Updated - Grace).TotalSeconds;
        var score = Score;
        while (elapsed > 0f && score > 0f)
        {
            var rank = RankOf(score);
            var rate = Decay[Math.Max(rank, 1)];
            var toFloor = rank > 0 ? (score - Thresholds[rank]) / rate : score / rate;
            if (elapsed <= toFloor)
                return score - rate * elapsed;

            elapsed -= toFloor;
            score = rank > 0 ? Thresholds[rank] - 0.001f : 0f;
        }

        return Math.Max(0f, score);
    }

    public int HitsAt(TimeSpan now)
    {
        return now - LastHit > HitChainWindow ? 0 : Hits;
    }
}
