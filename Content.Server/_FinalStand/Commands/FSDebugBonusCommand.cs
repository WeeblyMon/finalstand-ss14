using Content.Server._FinalStand.Leveling;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed class FSDebugBonusCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    public string Command => "fsdebugbonus";
    public string Description => "DEBUG: dump the wave-HUD bonus-summary computation for your held item.";
    public string Help => "fsdebugbonus  — run as the player whose held item you want to inspect";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player?.AttachedEntity is not { } mob)
        {
            shell.WriteError("No attached entity for this session.");
            return;
        }

        var result = _entMan.System<FSPlayerBonusSummarySystem>().DescribeFor(mob);
        shell.WriteLine(result);
    }
}
