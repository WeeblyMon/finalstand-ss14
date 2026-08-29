using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    ///     Whether the FinalStand preset and map are forced on every round reset.
    ///     Integration tests turn this off so the harness keeps control of the map and lobby cvars.
    /// </summary>
    public static readonly CVarDef<bool> FSAutoStart =
        CVarDef.Create("fs.autostart", true, CVar.SERVERONLY);
}
