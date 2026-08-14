using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class PauseWaveSpawnsCommand : LocalizedEntityCommands
{
    [Dependency] private WaveGameRuleSystem _waveRule = default!;

    public override string Command => "pausewavespawns";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _waveRule.ToggleSpawnPause(shell);
    }
}
