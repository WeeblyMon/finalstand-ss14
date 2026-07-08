using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> NPCMaxUpdates =
        CVarDef.Create("npc.max_updates", 128);

    public static readonly CVarDef<bool> NPCEnabled = CVarDef.Create("npc.enabled", true);

    /// <summary>
    ///     Should NPCs pathfind when steering. For debug purposes.
    /// </summary>
    public static readonly CVarDef<bool> NPCPathfinding = CVarDef.Create("npc.pathfinding", true);

    // HordeBrain — shared occupancy awareness for wave zombies
    public static readonly CVarDef<bool> HordeBrainEnabled =
        CVarDef.Create("npc.hordebrain_enabled", true);

    // How many zombies on a tile before it's considered occupied (can't route there).
    public static readonly CVarDef<int> HordeBrainOccupancyThreshold =
        CVarDef.Create("npc.hordebrain_occupancy_threshold", 2);

    // Retained for console tuning; no longer referenced in code after flow-field rework.
    public static readonly CVarDef<int> HordeBrainFlowTrigger =
        CVarDef.Create("npc.hordebrain_flow_trigger", 2);

    // Misalignment danger weight for FlowFieldSeek — how strongly the flow field overrides A*.
    public static readonly CVarDef<float> HordeBrainFlowWeight =
        CVarDef.Create("npc.hordebrain_flow_weight", 0.6f);

    // How many backward-nudge attempts before a stuck wave zombie is silently deleted.
    public static readonly CVarDef<int> HordeBrainNudgeLimit =
        CVarDef.Create("npc.hordebrain_nudge_limit", 3);
}
