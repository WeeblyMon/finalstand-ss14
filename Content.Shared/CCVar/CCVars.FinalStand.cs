using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> FSAutoStart =
        CVarDef.Create("fs.autostart", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> FSHudMigrated =
        CVarDef.Create("fs.hud_migrated", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
