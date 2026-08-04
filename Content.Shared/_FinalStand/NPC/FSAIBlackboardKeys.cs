// Blackboard key constants shared across FS wave-zombie HTN operators and systems.
namespace Content.Shared._FinalStand.NPC;

public static class FSAIBlackboardKeys
{
    public static readonly string BreachTarget        = "FSBreachTarget";
    public static readonly string BaitTarget          = "FSBaitTarget";
    public static readonly string AttackLockTimer     = "FSAttackLockTimer";
    public static readonly string LastPathProgress    = "FSLastPathProgress";
    public static readonly string PathProgressTimer   = "FSPathProgressTimer";
    public static readonly string LastAttacker        = "FSLastAttacker";
    public static readonly string RetaliationTimer    = "FSRetaliationTimer";
    public static readonly string CachedBreachScore   = "FSCachedBreachScore";
    public static readonly string PathZeroTimer       = "FSPathZeroTimer";
    public static readonly string BreachEvalFailCount = "FSBreachEvalFailCount";
    public static readonly string BreachCooldown      = "FSBreachCooldown";
    public static readonly string MazeCheckTimer      = "FSMazeCheckTimer";
}
