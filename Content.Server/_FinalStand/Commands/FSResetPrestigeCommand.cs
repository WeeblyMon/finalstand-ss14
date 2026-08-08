using Content.Server._FinalStand.Economy;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed class FSResetPrestigeCommand : IConsoleCommand
{
    public string Command => "fsresetprestige";
    public string Description => "Wipe all rows from fsprestige.db and reset every connected player's AP, level, and perks.";
    public string Help => "fsresetprestige — no arguments, affects all players";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var wallet = IoCManager.Resolve<IEntityManager>().System<FSPlayerWalletSystem>();
        var deleted = wallet.WipeAllPrestige();
        shell.WriteLine($"fsprestige.db wiped ({deleted} row(s) deleted). All connected players have been reset.");
    }
}
