using Content.Server._FinalStand.Economy;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Mind;
using Robust.Shared.Console;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSGiveCreditsCommand : IConsoleCommand
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private ISharedPlayerManager _playerManager = default!;

    public string Command => "fsgive";
    public string Description => "DEBUG: give credits to yourself or a named player.";
    public string Help => "fsgive [amount] [username]  — defaults: amount=10000, username=self";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var amount = 10000;
        if (args.Length >= 1 && !int.TryParse(args[0], out amount))
        {
            shell.WriteError($"Invalid amount '{args[0]}'.");
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

        if (session.AttachedEntity is not { } playerEntity)
        {
            shell.WriteError("Target player has no attached entity.");
            return;
        }

        var mindSystem = _entMan.System<SharedMindSystem>();
        if (!mindSystem.TryGetMind(playerEntity, out var mindId, out _))
        {
            shell.WriteError("Target player has no mind component.");
            return;
        }

        _entMan.System<FSPlayerWalletSystem>().GiveCredits(mindId, amount);
        shell.WriteLine($"Gave {amount} credits to {session.Name}.");
    }
}
