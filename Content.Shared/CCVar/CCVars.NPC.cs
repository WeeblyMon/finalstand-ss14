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

    // Master toggle for the wave-zombie support systems: flow field and stuck recovery.
    public static readonly CVarDef<bool> HordeBrainEnabled =
        CVarDef.Create("npc.hordebrain_enabled", true);

    // How many backward-nudge attempts before a stuck wave zombie is silently deleted.
    public static readonly CVarDef<int> HordeBrainNudgeLimit =
        CVarDef.Create("npc.hordebrain_nudge_limit", 3);
}
