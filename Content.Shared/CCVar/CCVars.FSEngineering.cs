using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> FSEngHealthBarCompact =
        CVarDef.Create("fs.eng_health_bar_compact", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
