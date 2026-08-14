using Content.Server._FinalStand.Research;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSGiveResearchPointsCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entMan = default!;

    public string Command => "fsgiverp";
    public string Description => "DEBUG: grant SCI research points (station-wide, feeds the active node or banks if none selected).";
    public string Help => "fsgiverp [amount]  — default: amount=5000";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var amount = 5000;
        if (args.Length >= 1 && !int.TryParse(args[0], out amount))
        {
            shell.WriteError($"Invalid amount '{args[0]}'.");
            return;
        }

        _entMan.System<FSResearchSystem>().GrantResearchPoints(amount, "fsgiverp");
        shell.WriteLine($"Granted {amount} research points.");
    }
}
