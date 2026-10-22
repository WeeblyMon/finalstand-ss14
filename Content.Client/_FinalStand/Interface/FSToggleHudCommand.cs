using Content.Client.Administration.Managers;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Client._FinalStand.Interface;

public sealed partial class FSToggleHudCommand : LocalizedCommands
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClientAdminManager _admin = default!;

    public override string Command => "fstogglehud";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_admin.IsActive())
        {
            shell.WriteError(Loc.GetString("cmd-fstogglehud-denied"));
            return;
        }

        var on = !_cfg.GetCVar(CCVars.FSClassicHud);
        _cfg.SetCVar(CCVars.FSClassicHud, on);
        _cfg.SaveToFile();
        shell.WriteLine(Loc.GetString(on ? "cmd-fstogglehud-on" : "cmd-fstogglehud-off"));
    }
}
