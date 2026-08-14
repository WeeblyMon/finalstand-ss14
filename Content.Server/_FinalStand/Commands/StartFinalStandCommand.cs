using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class StartFinalStandCommand : LocalizedEntityCommands
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameMapManager _mapManager = default!;
    [Dependency] private GameTicker _ticker = default!;

    public override string Command => "startfinalstand";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_ticker.TryFindGamePreset("finalstand", out var preset))
        {
            shell.WriteError("FinalStand preset not found. Is the prototype loaded?");
            return;
        }

        if (!_mapManager.CheckMapExists("FinalStandMap1"))
        {
            shell.WriteError("FinalStandMap1 not found. Is the prototype loaded?");
            return;
        }

        _cfg.SetCVar(CCVars.GameLobbyEnabled, true);
        _cfg.SetCVar(CCVars.GameMap, "FinalStandMap1");
        _ticker.SetGamePreset(preset, false);
        _ticker.RestartRound();

        shell.WriteLine("Final Stand round starting on FinalStandMap1.");
    }
}
