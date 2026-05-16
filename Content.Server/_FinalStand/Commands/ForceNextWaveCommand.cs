using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

// Fallback timer too long sometimes, use during testing, waves and stuff.

[AdminCommand(AdminFlags.Round)]
public sealed class ForceNextWaveCommand : LocalizedEntityCommands
{
    [Dependency] private readonly WaveGameRuleSystem _waveRule = default!;

    public override string Command => "forcenextwave";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        int? wave = args.Length > 0 && int.TryParse(args[0], out var n) ? n : null;
        _waveRule.ForceNextWave(shell, wave);
    }
}
