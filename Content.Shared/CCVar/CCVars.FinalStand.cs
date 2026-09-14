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

    /// <summary>
    ///     Whether this client has already been moved off the separated layout onto the reworked
    ///     HUD. ui.layout is ARCHIVE, so without this flag the migration would fire every launch
    ///     and a player could never deliberately stay on separated.
    /// </summary>
    public static readonly CVarDef<bool> FSHudMigrated =
        CVarDef.Create("fs.hud_migrated", false, CVar.CLIENTONLY | CVar.ARCHIVE);
}
