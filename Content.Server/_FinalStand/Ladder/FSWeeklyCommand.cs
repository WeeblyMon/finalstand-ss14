using System.Linq;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Ladder;

[AnyCommand]
public sealed partial class FSWeeklyCommand : LocalizedEntityCommands
{
    [Dependency] private FSWeeklyChallengeSystem _weekly = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override string Command => "weekly";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 1 && args[0].Equals("vote", StringComparison.OrdinalIgnoreCase))
        {
            if (!_weekly.TryStartVote(shell.Player, out var error))
                shell.WriteLine(error);
            return;
        }

        var challenge = _weekly.GetChallenge(DateTime.UtcNow);
        if (challenge == null)
        {
            shell.WriteLine(Loc.GetString("fs-weekly-none"));
            return;
        }

        var modifiers = string.Join(", ", challenge.Modifiers.Select(m => Loc.GetString(_proto.Index(m).Name)));
        shell.WriteLine(Loc.GetString("cmd-weekly-summary",
            ("name", Loc.GetString(challenge.Name)),
            ("description", Loc.GetString(challenge.Description)),
            ("difficulty", Loc.GetString(FSDifficultyRules.NameLocId(challenge.Difficulty))),
            ("modifiers", modifiers),
            ("wave", challenge.ClearWave),
            ("points", FSWeeklyRules.ClearPerkPoints)));
    }
}
