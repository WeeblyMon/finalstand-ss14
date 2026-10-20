using Content.Server._FinalStand.Research;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSResetResearchCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entMan = default!;

    public string Command => "fsresettech";
    public string Description => "DEBUG: relocks every SCI research node, clears progress, points, picks and queues.";
    public string Help => "fsresettech — no arguments; vanilla lathe recipes and bonuses already applied to existing " +
                          "guns, HMGs, mediguns and deployables stay until those are respawned";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var count = _entMan.System<FSResearchSystem>().ResetAllNodes();
        shell.WriteLine($"Reset {count} research node(s).");
    }
}
