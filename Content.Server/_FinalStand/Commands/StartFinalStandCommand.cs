using Content.Server.Administration;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

using Content.Server._FinalStand.GameTicking;

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
        if (!_ticker.TryFindGamePreset(FSAutoStartSystem.Preset, out var preset))
        {
            shell.WriteError(Loc.GetString("cmd-startfinalstand-no-preset", ("preset", FSAutoStartSystem.Preset.Id)));
            return;
        }

        if (!_mapManager.CheckMapExists(FSAutoStartSystem.Map))
        {
            shell.WriteError(Loc.GetString("cmd-startfinalstand-no-map", ("map", FSAutoStartSystem.Map.Id)));
            return;
        }

        _cfg.SetCVar(CCVars.GameLobbyEnabled, true);
        _cfg.SetCVar(CCVars.GameMap, FSAutoStartSystem.Map.Id);
        _ticker.SetGamePreset(preset, false);
        _ticker.RestartRound();

        shell.WriteLine(Loc.GetString("cmd-startfinalstand-starting", ("map", FSAutoStartSystem.Map.Id)));
    }
}
