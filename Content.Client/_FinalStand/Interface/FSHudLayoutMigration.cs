using Content.Client.UserInterface.Screens;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._FinalStand.Interface;

// The reworked HUD is built for the default screen. Anyone still on the separated layout is moved
// across once, then left alone - ui.layout is ARCHIVE, so re-applying this every launch would stop
// a player ever choosing separated on purpose.
public static class FSHudLayoutMigration
{
    public static void Run(IConfigurationManager cfg)
    {
        if (cfg.GetCVar(CCVars.FSHudMigrated))
            return;

        cfg.SetCVar(CCVars.FSHudMigrated, true);

        var current = cfg.GetCVar(CCVars.UILayout);
        if (Enum.TryParse<ScreenType>(current, out var screen) && screen == ScreenType.Separated)
            cfg.SetCVar(CCVars.UILayout, nameof(ScreenType.Default));
    }
}
