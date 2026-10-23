using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Tutorial;

[AnyCommand]
public sealed partial class FSTutorialCommand : LocalizedCommands
{
    [Dependency] private IEntityManager _entMan = default!;

    public override string Command => "tutorial";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("cmd-tutorial-no-player"));
            return;
        }

        var tutorial = _entMan.System<FSNewPlayerTipSystem>();
        switch (args.Length == 0 ? string.Empty : args[0].ToLowerInvariant())
        {
            case "on":
                tutorial.SetEnabled(player.UserId, true);
                shell.WriteLine(Loc.GetString("cmd-tutorial-on"));
                break;
            case "off":
                tutorial.SetEnabled(player.UserId, false);
                shell.WriteLine(Loc.GetString("cmd-tutorial-off"));
                break;
            case "replay":
                tutorial.Replay(player.UserId);
                shell.WriteLine(Loc.GetString("cmd-tutorial-replay"));
                break;
            default:
                shell.WriteLine(Loc.GetString(tutorial.WantsTips(player) ? "cmd-tutorial-status-on" : "cmd-tutorial-status-off"));
                break;
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        => args.Length == 1
            ? CompletionResult.FromHintOptions(new[] { "on", "off", "replay" }, Loc.GetString("cmd-tutorial-hint"))
            : CompletionResult.Empty;
}
