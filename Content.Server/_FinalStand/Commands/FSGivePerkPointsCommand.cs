using Content.Server._FinalStand.Economy;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSGivePerkPointsCommand : IConsoleCommand
{
    [Dependency] private ISharedPlayerManager _playerManager = default!;

    public string Command => "fsgiveap";
    public string Description => "Give (or take) perk points from yourself or another player.";
    public string Help => "fsgiveap <amount> [username]  — use a negative amount to take points away";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var amount))
        {
            shell.WriteError("Usage: fsgiveap <amount> [username]");
            return;
        }

        ICommonSession? session;
        if (args.Length >= 2)
        {
            if (!_playerManager.TryGetSessionByUsername(args[1], out session))
            {
                shell.WriteError($"Player '{args[1]}' not found.");
                return;
            }
        }
        else
        {
            session = shell.Player;
            if (session == null)
            {
                shell.WriteError("Run this command in-game or provide a username.");
                return;
            }
        }

        IoCManager.Resolve<IEntityManager>().System<FSPlayerWalletSystem>().GivePerkPoints(session, amount);

        var verb = amount >= 0 ? $"Gave +{amount}" : $"Took {amount}";
        shell.WriteLine($"{verb} perk points to {session.Name}.");
    }
}
