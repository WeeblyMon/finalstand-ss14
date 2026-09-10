using Content.Server.Fluids.EntitySystems;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Maps;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSSplashFlaskSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SmokeSystem _smoke = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private TurfSystem _turf = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FSChemCreditSystem _credit = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private readonly HashSet<Entity<FSFriendlyFireComponent>> _crewBuffer = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSplashFlaskComponent, LandEvent>(OnLand);
        SubscribeLocalEvent<FSAllyProjectileComponent, EmbedEvent>(OnEmbed);
    }

    private void OnEmbed(Entity<FSAllyProjectileComponent> ent, ref EmbedEvent args)
    {
        if (args.Shooter is { } shooter)
            _credit.RegisterDelivery(args.Embedded, shooter);
    }

    private void OnLand(Entity<FSSplashFlaskComponent> ent, ref LandEvent args)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution)
            || solution.Volume <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fs-splash-flask-empty"), ent);
            return;
        }

        var mapCoords = _xform.GetMapCoordinates(ent);

        if (!_map.TryFindGridAt(mapCoords, out var gridUid, out var grid)
            || !_map.TryGetTileRef(gridUid, grid, Transform(ent).Coordinates, out var tileRef)
            || _turf.IsSpace(tileRef))
        {
            return;
        }

        var payload = _solutions.SplitSolution(soln.Value, solution.Volume);
        var coords = _map.MapToGrid(gridUid, mapCoords);
        var cloud = Spawn(ent.Comp.CloudProto, coords.SnapToGrid());

        _smoke.StartSmoke(cloud, payload, ent.Comp.Duration, ent.Comp.SpreadAmount);

        if (args.User is { } thrower && AppliesCombatBuff(payload))
        {
            _crewBuffer.Clear();
            _lookup.GetEntitiesInRange(mapCoords, ent.Comp.SpreadAmount, _crewBuffer);

            foreach (var crew in _crewBuffer)
                _credit.RegisterDelivery(crew, thrower);
        }

        QueueDel(ent);
    }

    private bool AppliesCombatBuff(Solution payload)
    {
        foreach (var quantity in payload.Contents)
        {
            if (!_prototypes.TryIndex(quantity.Reagent.Prototype, out ReagentPrototype? reagent))
                continue;

            if (reagent.ReactiveEffects == null)
                continue;

            foreach (var (_, reactive) in reagent.ReactiveEffects)
            {
                foreach (var effect in reactive.Effects)
                {
                    if (effect is FSApplyCombatBuff)
                        return true;
                }
            }
        }

        return false;
    }
}
