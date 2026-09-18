using Content.Client.UserInterface.Screens;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._FinalStand.Interface;

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
