using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AnyCommand]
public sealed partial class RerollModifiersCommand : LocalizedEntityCommands
{
    [Dependency] private WaveGameRuleSystem _waveRule = default!;

    public override string Command => "rerollmodifiers";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_waveRule.TryStartRerollVote(shell.Player, out var error))
            shell.WriteLine(error);
    }
}
