using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> HeightAdjustModifiesHitbox =
        CVarDef.Create("heightadjust.modifies_hitbox", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> HeightAdjustModifiesZoom =
        CVarDef.Create("heightadjust.modifies_zoom", false, CVar.SERVERONLY);

    public static readonly CVarDef<bool> HeightAdjustModifiesBloodstream =
        CVarDef.Create("heightadjust.modifies_bloodstream", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> HeightAdjustModifiesSprinting =
        CVarDef.Create("heightadjust.modifies_sprinting", true, CVar.SERVERONLY);

    public static readonly CVarDef<bool> HeightAdjustModifiesFlight =
        CVarDef.Create("heightadjust.modifies_flight", true, CVar.SERVERONLY);
}
