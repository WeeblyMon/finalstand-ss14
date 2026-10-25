using Content.Server._FinalStand.Admin;
using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed partial class FSCustomZombieCommand : IConsoleCommand
{
    [Dependency] private EuiManager _euiManager = default!;

    public string Command => "fszombieui";
    public string Description => "Opens the custom zombie spawner for building a buffed mini-boss.";
    public string Help => "fszombieui";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player == null)
        {
            shell.WriteError("Run this command in-game.");
            return;
        }

        _euiManager.OpenEui(new FSCustomZombieEui(), shell.Player);
    }
}
