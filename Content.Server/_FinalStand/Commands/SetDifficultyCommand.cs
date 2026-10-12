using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Administration;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class SetDifficultyCommand : LocalizedEntityCommands
{
    [Dependency] private WaveGameRuleSystem _waveRule = default!;

    public override string Command => "setdifficulty";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !Enum.TryParse<FSDifficulty>(args[0], true, out var difficulty))
        {
            shell.WriteError(Help);
            return;
        }

        if (!_waveRule.TrySetDifficulty(difficulty))
            shell.WriteError(Loc.GetString("fs-difficulty-locked"));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length == 1
            ? CompletionResult.FromOptions(Enum.GetNames<FSDifficulty>().Select(n => n.ToLowerInvariant()))
            : CompletionResult.Empty;
    }
}
