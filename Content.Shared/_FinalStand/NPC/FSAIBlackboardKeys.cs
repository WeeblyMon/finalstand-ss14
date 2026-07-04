namespace Content.Shared._FinalStand.NPC;

public static class FSAIBlackboardKeys
{
    // EntityUid of the current breach target structure
    public static readonly string BreachTarget = "FSBreachTarget";

    // EntityUid of an active bait decoy that should distract this zombie
    public static readonly string BaitTarget = "FSBaitTarget";

    // Countdown timer for soft attack lock (float, seconds remaining)
    public static readonly string AttackLockTimer = "FSAttackLockTimer";

    // How long since zombie last made meaningful path progress (Vector2, last world position)
    // TODO(finalstand): switch to waypoint-based progress if nav API exposes it cleanly
    public static readonly string LastPathProgress = "FSLastPathProgress";

    // How long the zombie has been stalled with no progress (float, seconds)
    public static readonly string PathProgressTimer = "FSPathProgressTimer";

    // EntityUid of player who last damaged this zombie (for retaliation)
    public static readonly string LastAttacker = "FSLastAttacker";

    // How long before the retaliation window expires (float, seconds countdown)
    public static readonly string RetaliationTimer = "FSRetaliationTimer";

    // Cached score of the current breach target — used for the 2× better-target interrupt
    public static readonly string CachedBreachScore = "FSCachedBreachScore";

    // Consecutive seconds the zombie has had no active steering path (float).
    // Breach mode only triggers after this exceeds 0.5s to skip normal path-recalculation gaps.
    public static readonly string PathZeroTimer = "FSPathZeroTimer";

    // How many consecutive times EvaluateBreachTarget found no candidate above the score threshold.
    // Relaxes the threshold from 0.05 to 0.01 after 5 failures to break infinite stall loops.
    public static readonly string BreachEvalFailCount = "FSBreachEvalFailCount";

    // Cooldown after a breach target is cleared — blocks stall detection from immediately
    // re-targeting adjacent entities (plants, lights) while zombie replans and moves away.
    public static readonly string BreachCooldown = "FSBreachCooldown";

    // Rate-limit timer for the long-path (maze) breach check (float, seconds).
    public static readonly string MazeCheckTimer = "FSMazeCheckTimer";
}
