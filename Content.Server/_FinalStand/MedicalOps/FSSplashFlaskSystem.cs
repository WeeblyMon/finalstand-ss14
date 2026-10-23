using System.Linq;
using Content.Server.Fluids.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

// Field potions in a flask land as a chem zone; anything else a doctor pours in still clouds as ordinary smoke.
public sealed partial class FSSplashFlaskSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private FSChemZoneSystem _zones = default!;

    private static readonly EntProtoId SmokeProto = "FSSplashCloud";
    private const float SmokeDuration = 10f;
    private const int SmokeSpread = 6;

    private static readonly SoundSpecifier EmptySound =
        new SoundPathSpecifier("/Audio/Effects/Fluids/slosh.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSplashFlaskComponent, LandEvent>(OnLand);
    }

    private void OnLand(Entity<FSSplashFlaskComponent> ent, ref LandEvent args)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution)
            || solution.Volume <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fs-splash-flask-empty"), ent);
            _audio.PlayPvs(EmptySound, ent);
            return;
        }

        var mapCoords = _xform.GetMapCoordinates(ent);
        if (!_map.TryFindGridAt(mapCoords, out var gridUid, out var grid)
            || !_map.TryGetTileRef(gridUid, grid, mapCoords.Position, out var tile)
            || _turf.IsSpace(tile))
            return;

        var payload = _solutions.SplitSolution(soln.Value, solution.Volume);
        var potions = payload.Contents
            .Select(quantity => new ProtoId<ReagentPrototype>(quantity.Reagent.Prototype))
            .Where(reagent => _zones.KindOf(reagent) != null)
            .Distinct()
            .ToList();

        if (potions.Count > 0)
        {
            var label = potions.Count == 1 && _prototypes.TryIndex(potions[0], out var reagent) ? reagent.LocalizedName : null;
            _zones.SpawnZone(mapCoords, potions, args.User, null, false, label);

            foreach (var potion in potions)
                payload.RemoveReagent(potion.Id, payload.GetTotalPrototypeQuantity(potion.Id));
        }

        if (payload.Volume > 0)
            Smoke(_map.MapToGrid(gridUid, mapCoords), payload);

        QueueDel(ent);
    }

    private void Smoke(EntityCoordinates coords, Solution payload)
    {
        var cloud = Spawn(SmokeProto, coords.SnapToGrid());
        _smoke.StartSmoke(cloud, payload, SmokeDuration, SmokeSpread);
    }
}
