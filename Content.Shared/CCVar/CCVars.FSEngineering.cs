using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> FSEngHealthBarCompact =
        CVarDef.Create("fs.eng_health_bar_compact", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngGhostCard =
        CVarDef.Create("fs.eng_ghost_card", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngDeniedFeedback =
        CVarDef.Create("fs.eng_denied_feedback", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngWorldChips =
        CVarDef.Create("fs.eng_world_chips", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngRepairFeedback =
        CVarDef.Create("fs.eng_repair_feedback", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngBuildPop =
        CVarDef.Create("fs.eng_build_pop", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngMechHudMotion =
        CVarDef.Create("fs.eng_mech_hud_motion", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngRangePreview =
        CVarDef.Create("fs.eng_range_preview", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngWaveReport =
        CVarDef.Create("fs.eng_wave_report", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngOwnerAlerts =
        CVarDef.Create("fs.eng_owner_alerts", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngFirstHints =
        CVarDef.Create("fs.eng_first_hints", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> FSEngHintsSeen =
        CVarDef.Create("fs.eng_hints_seen", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSEngToolCard =
        CVarDef.Create("fs.eng_tool_card", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> FSEngSfxVolume =
        CVarDef.Create("fs.eng_sfx_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> FSMatDropBasePlayers =
        CVarDef.Create("fs.mat_drop_base_players", 4, CVar.SERVERONLY);

    public static readonly CVarDef<float> FSMatDropPerPlayer =
        CVarDef.Create("fs.mat_drop_per_player", 0.15f, CVar.SERVERONLY);

    public static readonly CVarDef<int> FSMatDropBaseWave =
        CVarDef.Create("fs.mat_drop_base_wave", 5, CVar.SERVERONLY);

    public static readonly CVarDef<float> FSMatDropPerWave =
        CVarDef.Create("fs.mat_drop_per_wave", 0.04f, CVar.SERVERONLY);

    public static readonly CVarDef<float> FSMatDropBonusCap =
        CVarDef.Create("fs.mat_drop_bonus_cap", 1.5f, CVar.SERVERONLY);

    public static readonly CVarDef<float> FSMatDropMergeRange =
        CVarDef.Create("fs.mat_drop_merge_range", 1.5f, CVar.SERVERONLY);
}
