using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._FinalStand.Interface;

public static class FSClassicHud
{
    public static bool Enabled => IoCManager.Resolve<IConfigurationManager>().GetCVar(CCVars.FSClassicHud);
}
