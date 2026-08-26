using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server._FinalStand.GameTicking;

// activates FinalStand preset and map on every round reset — no admin command needed
public sealed partial class FSAutoStartSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameMapManager _mapManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        Log.Debug($"[FSAutoStart] RunLevel changed: {ev.Old} → {ev.New}");

        if (ev.New != GameRunLevel.PreRoundLobby)
            return;

        // Integration tests own the map and lobby cvars; forcing ours here fights the harness.
        if (!_cfg.GetCVar(CCVars.FSAutoStart))
            return;

        if (!_ticker.TryFindGamePreset("finalstand", out var preset))
        {
            Log.Error("[FSAutoStart] FinalStand preset not found — auto-start skipped.");
            return;
        }

        if (!_mapManager.CheckMapExists("FinalStandMap1"))
        {
            Log.Error("[FSAutoStart] FinalStandMap1 map not found — auto-start skipped.");
            return;
        }

        _cfg.SetCVar(CCVars.GameLobbyEnabled, true);
        _cfg.SetCVar(CCVars.GameMap, "FinalStandMap1");
        _cfg.SetCVar(CCVars.ArrivalsShuttles, false); // FINALSTAND: arrivals shuttle not used, players spawn via cryo
        _ticker.SetGamePreset(preset);
        Log.Info("[FSAutoStart] FinalStand preset and map applied. Waiting for startround.");
    }
}
