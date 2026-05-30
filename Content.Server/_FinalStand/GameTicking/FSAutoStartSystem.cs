using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._FinalStand.GameTicking;

// activates FinalStand preset and map on every round reset — no admin command needed
public sealed class FSAutoStartSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IGameMapManager _mapManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New != GameRunLevel.PreRoundLobby)
            return;

        if (!_ticker.TryFindGamePreset("finalstand", out var preset))
        {
            Logger.ErrorS("finalstand.autostart", "FinalStand preset not found — auto-start skipped.");
            return;
        }

        if (!_mapManager.CheckMapExists("FinalStandMap1"))
        {
            Logger.ErrorS("finalstand.autostart", "FinalStandMap1 map not found — auto-start skipped.");
            return;
        }

        _cfg.SetCVar(CCVars.GameLobbyEnabled, true);
        _cfg.SetCVar(CCVars.GameMap, "FinalStandMap1");
        _ticker.SetGamePreset(preset);
    }
}
