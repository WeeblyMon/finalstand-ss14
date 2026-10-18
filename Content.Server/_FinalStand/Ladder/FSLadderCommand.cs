using System.Linq;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Ladder;

[AnyCommand]
public sealed partial class FSLadderCommand : LocalizedEntityCommands
{
    [Dependency] private FSLadderSystem _ladder = default!;

    public override string Command => "ladder";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var difficulty = FSDifficulty.Normal;
        var bracket = FSPlayerBracket.Crew;

        if (args.Length > 0 && !Enum.TryParse(args[0], true, out difficulty)
            || args.Length > 1 && !Enum.TryParse(args[1], true, out bracket)
            || !Enum.IsDefined(difficulty) || !Enum.IsDefined(bracket))
        {
            shell.WriteError(Help);
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-ladder-header",
            ("difficulty", Loc.GetString(FSDifficultyRules.NameLocId(difficulty))),
            ("bracket", Loc.GetString(FSLadder.BracketLocId(bracket)))));

        var runs = _ladder.GetTopRuns(difficulty, bracket);
        if (runs.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-ladder-empty"));
            return;
        }

        for (var i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            shell.WriteLine(Loc.GetString(run.Victory ? "cmd-ladder-row-victory" : "cmd-ladder-row",
                ("rank", i + 1),
                ("waves", run.Waves),
                ("time", TimeSpan.FromSeconds(run.DurationSeconds).ToString(@"h\:mm\:ss")),
                ("players", run.Players)));
        }
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromOptions(Enum.GetNames<FSDifficulty>().Select(n => n.ToLowerInvariant())),
            2 => CompletionResult.FromOptions(Enum.GetNames<FSPlayerBracket>().Select(n => n.ToLowerInvariant())),
            _ => CompletionResult.Empty,
        };
    }
}
