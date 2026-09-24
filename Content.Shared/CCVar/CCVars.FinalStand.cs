using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> FSAutoStart =
        CVarDef.Create("fs.autostart", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> FSHudMigrated =
        CVarDef.Create("fs.hud_migrated", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSUiScaleMigrated =
        CVarDef.Create("fs.ui_scale_migrated", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> FSWeaponsVolume =
        CVarDef.Create("fs.weapons_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSAudioDynamics =
        CVarDef.Create("fs.audio_dynamics", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> FSWeaponsHeadroom =
        CVarDef.Create("fs.weapons_headroom", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
