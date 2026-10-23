using Content.Shared.CCVar;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Client._FinalStand.Interface;

public static class FSHudLayoutMigration
{
    public static void Run(IConfigurationManager cfg)
    {
        MigrateUiScale(cfg);
        MigrateChatSize(cfg);
    }

    private static void MigrateChatSize(IConfigurationManager cfg)
    {
        if (cfg.GetCVar(CCVars.FSChatSizeMigrated))
            return;

        cfg.SetCVar(CCVars.FSChatSizeMigrated, true);
        cfg.SetCVar(CCVars.FSChatSize, cfg.GetCVar(CCVars.DefaultScreenChatSize));
        cfg.SetCVar(CCVars.DefaultScreenChatSize, "");
        cfg.SaveToFile();
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
