using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.GameTicking.Commands;

/// <summary>
/// Forces the round to start immediately, bypassing lobby timer and PreRoundLobby requirement.
/// </summary>
[AdminCommand(AdminFlags.Round)]
public sealed class StartRoundNowCommand : LocalizedEntityCommands
{
    [Dependency] private readonly GameTicker _gameTicker = default!;

    public override string Command => "startroundnow";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        switch (_gameTicker.RunLevel)
        {
            case GameRunLevel.PreRoundLobby:
                _gameTicker.StartRound(force: true);
                break;
            case GameRunLevel.InRound:
                shell.WriteLine("Round is already running.");
                break;
            default:
                _gameTicker.RestartRound();
                break;
        }
    }
}
