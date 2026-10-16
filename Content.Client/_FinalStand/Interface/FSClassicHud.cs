using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Client._FinalStand.Interface;

public static class FSClassicHud
{
    private static bool? _enabled;

    public static bool Enabled => _enabled ??= IoCManager.Resolve<IConfigurationManager>().GetCVar(CCVars.FSClassicHud);
}
