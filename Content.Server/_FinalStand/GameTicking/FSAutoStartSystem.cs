using Content.Server.GameTicking;
using Content.Server.GameTicking.Presets;
using Content.Server.Maps;
using Content.Shared.Maps;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.GameTicking;

// activates FinalStand preset and map on every round reset — no admin command needed
public sealed partial class FSAutoStartSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private IGameMapManager _mapManager = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private static readonly ProtoId<GamePresetPrototype> Preset = "FinalStand";
    private static readonly ProtoId<GameMapPrototype> Map = "FinalStandMap1";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        Log.Debug($"RunLevel changed: {ev.Old} → {ev.New}");

        if (ev.New != GameRunLevel.PreRoundLobby)
            return;

        // Integration tests own the map and lobby cvars; forcing ours here fights the harness.
        if (!_cfg.GetCVar(CCVars.FSAutoStart))
            return;

        if (!_ticker.TryFindGamePreset(Preset, out var preset))
        {
            Log.Error($"Preset {Preset} not found, skipping auto-start.");
            return;
        }

        if (!_mapManager.CheckMapExists(Map))
        {
            Log.Error($"Map {Map} not found, skipping auto-start.");
            return;
        }

        _cfg.SetCVar(CCVars.GameMap, Map.Id);
        _cfg.SetCVar(CCVars.ArrivalsShuttles, false); // FINALSTAND: arrivals shuttle not used, players spawn via cryo
        _ticker.SetGamePreset(preset);
        Log.Info("FinalStand preset and map applied. Waiting for startround.");
    }
}
