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
}
