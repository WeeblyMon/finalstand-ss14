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

    // Master toggle for the wave-zombie pathing support systems: HordeFlowFieldSystem and
    // FSStuckRecoverySystem.
    public static readonly CVarDef<bool> WaveZombiePathingEnabled =
        CVarDef.Create("npc.wave_zombie_pathing_enabled", true);

    // How many backward-nudge attempts before a stuck wave zombie is silently deleted.
    public static readonly CVarDef<int> WaveZombieStuckNudgeLimit =
        CVarDef.Create("npc.wave_zombie_stuck_nudge_limit", 3);
}
