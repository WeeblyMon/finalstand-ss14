using Content.Client.UserInterface.Screens;
using Content.Shared.CCVar;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Client._FinalStand.Interface;

public static class FSHudLayoutMigration
{
    public static void Run(IConfigurationManager cfg)
    {
        MigrateLayout(cfg);
        MigrateUiScale(cfg);
    }

    private static void MigrateLayout(IConfigurationManager cfg)
    {
        if (cfg.GetCVar(CCVars.FSHudMigrated))
            return;

        cfg.SetCVar(CCVars.FSHudMigrated, true);

        var current = cfg.GetCVar(CCVars.UILayout);
        if (Enum.TryParse<ScreenType>(current, out var screen) && screen == ScreenType.Separated)
            cfg.SetCVar(CCVars.UILayout, nameof(ScreenType.Default));
    }

    // The engine default of 0 follows OS DPI scale; the HUD was never sized for that.
    private static void MigrateUiScale(IConfigurationManager cfg)
    {
        if (cfg.GetCVar(CCVars.FSUiScaleMigrated))
            return;

        cfg.SetCVar(CCVars.FSUiScaleMigrated, true);
        cfg.SetCVar(CVars.DisplayUIScale, 1f);
    }
}
