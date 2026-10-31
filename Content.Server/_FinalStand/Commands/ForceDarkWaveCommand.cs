using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class ForceDarkWaveCommand : LocalizedEntityCommands
{
    [Dependency] private FSDarkWaveSystem _darkWave = default!;

    public override string Command => "forcedarkwave";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_darkWave.TryForce(out var armedNow))
        {
            shell.WriteError(Loc.GetString("cmd-wave-rule-not-active"));
            return;
        }

        shell.WriteLine(Loc.GetString(armedNow ? "cmd-forcedarkwave-now" : "cmd-forcedarkwave-next"));
    }
}
