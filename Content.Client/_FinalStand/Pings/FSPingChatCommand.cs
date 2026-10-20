using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Client._FinalStand.Pings;

[AnyCommand]
public sealed partial class FSPingChatCommand : LocalizedCommands
{
    [Dependency] private IConfigurationManager _cfg = default!;

    public override string Command => "pingchat";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var on = !_cfg.GetCVar(CCVars.FSPingChat);
        _cfg.SetCVar(CCVars.FSPingChat, on);
        _cfg.SaveToFile();
        shell.WriteLine(Loc.GetString(on ? "cmd-pingchat-on" : "cmd-pingchat-off"));
    }
}
