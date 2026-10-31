using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class ForceNextWaveCommand : LocalizedEntityCommands
{
    [Dependency] private WaveGameRuleSystem _waveRule = default!;

    public override string Command => "forcenextwave";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        int? target = null;
        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out var parsed) || parsed < 1)
            {
                shell.WriteError(Loc.GetString("shell-argument-must-be-number"));
                return;
            }

            target = parsed;
        }

        if (_waveRule.IsWon())
        {
            shell.WriteError(Loc.GetString("cmd-forcenextwave-won"));
            return;
        }

        if (_waveRule.ForceNextWave(target) is not { } wave)
        {
            shell.WriteError(Loc.GetString("cmd-wave-rule-not-active"));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-forcenextwave-done", ("wave", wave)));
    }
}
