using System.Linq;
using Content.Server._FinalStand.Deployables;
using Content.Server._FinalStand.Effects;
using Content.Server._FinalStand.Emplacement;
using Content.Server._FinalStand.Engineering;
using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Materials;
using Content.Shared._FinalStand.Placement;
using Content.Shared._FinalStand.RCD;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Stacks;
using Content.Shared._FinalStand.Particles;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Map.Components;
using Content.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.RCD;

public sealed partial class FSRCDEngineerOnlySystem
{
    [Dependency] private FSPlacementRuleSystem _placementRules = default!;
    [Dependency] private FSDeployableSystem _deployable = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSWorkSoundSystem _workSound = default!;

    private static readonly SoundSpecifier[] BuildSounds =
    {
        new SoundPathSpecifier("/Audio/Items/drill_use.ogg"),
        new SoundPathSpecifier("/Audio/Items/ratchet.ogg"),
    };
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private Content.Shared.Maps.TurfSystem _turf = default!;
    [Dependency] private FSBreachEvaluator _breach = default!;
    [Dependency] private FSCarriedMaterialSystem _materials = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private FSEngineeringResearchSystem _research = default!;
    [Dependency] private FSEngineeringTelemetrySystem _telemetry = default!;
    [Dependency] private FSFlairSystem _flair = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly ProtoId<FSParticleEmitterPrototype> DustPuff = "FSDustPuff";

    private const float RefundFraction = 0.5f;
    private const float SalvageRefundFraction = 0.75f;
    private static readonly SoundSpecifier PlacedSound = new SoundPathSpecifier("/Audio/Effects/thunk.ogg", AudioParams.Default.WithVariation(0.1f));
    private const float RapidAssemblyMultiplier = 0.75f;
    private const float ChiefCapMultiplier = 1.5f;
    private const float ThreatRange = 5f;
    private const float ThreatDelayMultiplier = 3f;
    // A fixture box wider or taller than this covers more than the tile it is placed on.
    private const float SingleTileSpan = 1.1f;
    // Lets a half-extent sitting just under a tile edge still reach that tile.
    private const float FootprintSlack = 0.05f;

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _threats = new();
    private readonly Dictionary<EntityUid, Dictionary<string, int>> _builtByMind = new();


    public string? GetBlockReason(EntityUid rcd, FSEngineerOnlyRCDComponent comp, EntityUid user, EntityCoordinates location)
    {
        if (!_engineering.IsEngineering(user))
            return Loc.GetString("fs-rcd-engineers-only");

        if (!TryComp<RCDComponent>(rcd, out var rcdComp))
            return null;

        var rcdProto = _proto.Index(rcdComp.ProtoId);
        if (rcdProto.Mode is not (RcdMode.ConstructObject or RcdMode.ConstructTile))
            return null;

        if (_placementRules.HasNoBuildMarker(location))
            return Loc.GetString("fs-rcd-build-blocked");

        if (!_proto.TryIndex<FSBuildRecipePrototype>(rcdComp.ProtoId.Id, out var recipe))
            return null;

        Direction? facing = rcdProto.AllowMultiDirection ? rcdComp.ConstructionDirection : null;

        if (recipe.Research is { } node && !_research.Has(node))
            return Loc.GetString("fs-rcd-needs-research");

        if (recipe.Station)
            return MissingMaterials(user, recipe);

        if (!_placementRules.IsTileBuildable(location, blocksPath: true) || FootprintBlocked(rcdProto, location))
            return Loc.GetString("fs-rcd-in-the-way");

        if (_breach.IsAdjacentToSpace(location))
            return Loc.GetString("fs-rcd-next-to-space");

        if (_deployable.IsOccupied(location, facing, ignoreMines: true))
            return Loc.GetString("fs-deployable-occupied");

        var built = CountBuilt();
        if (built >= comp.StationCap)
            return Loc.GetString("fs-rcd-station-cap", ("used", built), ("max", comp.StationCap));

        if (recipe.CapGroup is { } group && comp.Caps.TryGetValue(group, out var baseMax))
        {
            var max = CapFor(baseMax, user);
            var used = BuiltBy(user).GetValueOrDefault(group);
            if (used >= max)
                return Loc.GetString("fs-rcd-cap",
                    ("group", Loc.GetString("fs-rcd-cap-group-" + group)), ("used", used), ("max", max));
        }

        return MissingMaterials(user, recipe);
    }

    private bool FootprintBlocked(RCDPrototype piece, EntityCoordinates location)
    {
        if (piece.Prototype is not { } protoId
            || !_proto.TryIndex<EntityPrototype>(protoId, out var proto)
            || !proto.TryGetComponent<FixturesComponent>(out var fixtures, Factory)
            || fixtures.Fixtures.Count == 0)
            return false;

        Box2? bounds = null;
        foreach (var fixture in fixtures.Fixtures.Values)
        {
            var aabb = fixture.Shape.ComputeAABB(Robust.Shared.Physics.Transform.Empty, 0);
            bounds = bounds is { } existing ? existing.Union(aabb) : aabb;
        }

        if (bounds is not { } box || box.Width <= SingleTileSpan && box.Height <= SingleTileSpan)
            return false;

        if (_transform.GetGrid(location) is not { } grid || !TryComp<MapGridComponent>(grid, out var gridComp))
            return true;

        var center = _mapSystem.TileIndicesFor(grid, gridComp, location);
        var reachX = (int) MathF.Floor(box.Width / 2f + FootprintSlack);
        var reachY = (int) MathF.Floor(box.Height / 2f + FootprintSlack);
        for (var dx = -reachX; dx <= reachX; dx++)
        {
            for (var dy = -reachY; dy <= reachY; dy++)
            {
                var tile = center + new Vector2i(dx, dy);
                if (!_mapSystem.TryGetTileRef(grid, gridComp, tile, out var tileRef)
                    || tileRef.Tile.IsEmpty
                    || _turf.IsTileBlocked(grid, tile, CollisionGroup.Impassable | CollisionGroup.HighImpassable, gridComp)
                    || !_placementRules.IsTileBuildable(_mapSystem.GridTileToLocal(grid, gridComp, tile), blocksPath: true))
                    return true;
            }
        }

        var here = _transform.ToMapCoordinates(location).Position;
        foreach (var other in _lookup.GetEntitiesInRange(location, MathF.Max(box.Width, box.Height)))
        {
            if (MetaData(other).EntityPrototype?.ID != protoId)
                continue;

            var delta = _transform.GetWorldPosition(other) - here;
            if (MathF.Abs(delta.X) < box.Width && MathF.Abs(delta.Y) < box.Height)
                return true;
        }

        return false;
    }

    private string? MissingMaterials(EntityUid user, FSBuildRecipePrototype recipe)
    {
        var missing = new List<string>();
        foreach (var (material, need) in recipe.Materials)
        {
            var have = Available(user, material);
            if (have < need)
                missing.Add(Loc.GetString("fs-rcd-need-entry", ("need", need), ("have", have),
                    ("name", Loc.GetString(_proto.Index(material).Name))));
        }

        return missing.Count > 0
            ? Loc.GetString("fs-rcd-need-materials", ("list", string.Join(", ", missing)))
            : null;
    }

    private void OnDelay(Entity<FSEngineerOnlyRCDComponent> ent, ref FSRCDDelayEvent args)
    {
        if (!TryComp<RCDComponent>(ent, out var rcdComp) || _proto.Index(rcdComp.ProtoId).Mode != RcdMode.ConstructObject)
            return;

        if (_research.Has(FSEngineeringNodes.RapidAssembly))
            args.Delay *= RapidAssemblyMultiplier;

        _threats.Clear();
        _lookup.GetEntitiesInRange(_transform.ToMapCoordinates(args.Location), ThreatRange, _threats);
        foreach (var zombie in _threats)
        {
            if (!_mobState.IsDead(zombie) && _interaction.InRangeUnobstructed(zombie.Owner, args.Location, ThreatRange))
            {
                args.Delay *= ThreatDelayMultiplier;
                _popup.PopupEntity(Loc.GetString("fs-rcd-under-threat"), ent, args.User, PopupType.SmallCaution);
                break;
            }
        }

        var piece = _proto.Index(rcdComp.ProtoId);
        var direction = rcdComp.ConstructionDirection;
        var rotation = piece.Rotation == RcdRotation.User ? direction.ToAngle() : Angle.Zero;
        _flair.Hologram(args.Location, piece.ID, args.Delay, rotation);
        _workSound.Start(ent, args.Location, BuildSounds, args.Delay);
    }

    private void OnBuilt(Entity<FSEngineerOnlyRCDComponent> ent, ref FSRCDBuiltEvent args)
    {
        var protoId = MetaData(args.Built).EntityPrototype?.ID;
        if (protoId == null || !_proto.TryIndex<FSBuildRecipePrototype>(protoId, out var recipe))
            return;

        if (!TryCommit(ent, args.User, recipe))
        {
            QueueDel(args.Built);
            _popup.PopupEntity(Loc.GetString("fs-rcd-materials-gone"), ent, args.User, PopupType.Medium);
            return;
        }

        _telemetry.RecordSpend("rcd", recipe.Materials);
        ShowSpent(recipe, Transform(args.Built).Coordinates, args.User);
        if (recipe.Station)
            return;

        _audio.PlayPvs(recipe.LandSound ?? PlacedSound, args.Built, AudioParams.Default.WithVariation(0.08f));
        _flair.Burst(DustPuff, args.Built);
        RaiseNetworkEvent(new FSBuildLandedEvent(GetNetEntity(args.Built)), Filter.Pvs(args.Built, entityManager: EntityManager));

        var directional = _proto.TryIndex<RCDPrototype>(protoId, out var rcdProto)
            && rcdProto.AllowMultiDirection;

        if (!Transform(args.Built).Anchored && !HasComp<PullableComponent>(args.Built))
            _transform.AnchorEntity(args.Built);

        var deployedBy = EnsureComp<FSDeployedByComponent>(args.Built);
        deployedBy.OwnerMind = _mind.TryGetMind(args.User, out var mindId, out _) ? mindId : null;
        deployedBy.DeployedBy = args.User;
        deployedBy.SourceProto = protoId;
        deployedBy.Facing = directional ? args.Direction : null;

        RefreshUsage(ent, args.User);
    }

    private void OnTileBuilt(Entity<FSEngineerOnlyRCDComponent> ent, ref FSRCDTileBuiltEvent args)
    {
        if (RecipeFor(ent) is not { } recipe)
            return;

        if (!TryCommit(ent, args.User, recipe))
        {
            _popup.PopupEntity(Loc.GetString("fs-rcd-materials-gone"), ent, args.User, PopupType.Medium);
            return;
        }

        _telemetry.RecordSpend("rcd", recipe.Materials);
        ShowSpent(recipe, args.Location, args.User);
    }

    private void ShowSpent(FSBuildRecipePrototype recipe, EntityCoordinates at, EntityUid user)
    {
        var spent = new List<string>();
        foreach (var (material, amount) in recipe.Materials)
            spent.Add(Loc.GetString("fs-rcd-spent", ("amount", amount), ("name", Loc.GetString(_proto.Index(material).Name))));
        _popup.PopupCoordinates(string.Join("  ", spent), at, user, PopupType.Small);
    }

    public void RefreshUsage(Entity<FSEngineerOnlyRCDComponent> rcd, EntityUid user)
    {
        ApplyUsage(rcd, user, CountBuilt());
    }

    private int CountBuilt()
    {
        _builtByMind.Clear();
        var station = 0;
        var query = EntityQueryEnumerator<FSDeployedByComponent>();
        while (query.MoveNext(out var uid, out var deployedBy))
        {
            if (TerminatingOrDeleted(uid) || deployedBy.SourceProto.Id is not { } source || !_proto.TryIndex<FSBuildRecipePrototype>(source, out var recipe))
                continue;

            station++;
            if (deployedBy.OwnerMind is not { } owner || recipe.CapGroup is not { } group)
                continue;

            if (!_builtByMind.TryGetValue(owner, out var groups))
                _builtByMind[owner] = groups = new Dictionary<string, int>();
            groups[group] = groups.GetValueOrDefault(group) + 1;
        }

        return station;
    }

    private IReadOnlyDictionary<string, int> BuiltBy(EntityUid user)
    {
        return _mind.TryGetMind(user, out var mindId, out _) && _builtByMind.TryGetValue(mindId, out var groups)
            ? groups
            : new Dictionary<string, int>();
    }

    private void ApplyUsage(Entity<FSEngineerOnlyRCDComponent> rcd, EntityUid user, int station)
    {
        var used = new Dictionary<string, int>(BuiltBy(user));
        var silo = SiloStockFor(user, rcd.Comp);
        var max = CapsFor(rcd.Comp, user);
        var locked = LockedPieces(rcd.Comp);
        if (station == rcd.Comp.StationUsed
            && DictionaryEquals(used, rcd.Comp.CapsUsed)
            && DictionaryEquals(max, rcd.Comp.CapsMax)
            && DictionaryEquals(silo, rcd.Comp.SiloStock)
            && locked.SequenceEqual(rcd.Comp.Locked))
            return;

        rcd.Comp.Locked = locked;
        rcd.Comp.StationUsed = station;
        rcd.Comp.CapsUsed = used;
        rcd.Comp.CapsMax = max;
        rcd.Comp.SiloStock = silo;
        Dirty(rcd);
    }

    public string? TakeDownBlock(EntityUid user, EntityUid target)
    {
        if (HasComp<FSEmplacementPieceComponent>(target) || HasComp<FSEmplacementComponent>(target))
            return Loc.GetString("fs-emplacement-pack-instead");

        return CanTakeDown(user, target) ? null : Loc.GetString("fs-rcd-not-yours");
    }

    private bool CanTakeDown(EntityUid user, EntityUid target)
    {
        if (!TryComp<FSDeployedByComponent>(target, out var deployedBy)
            || deployedBy.OwnerMind is not { } owner
            || _engineering.IsChiefEngineer(user))
            return true;

        if (_mind.TryGetMind(user, out var mindId, out _) && mindId == owner)
            return true;

        return !TryComp<MindComponent>(owner, out var ownerMind)
               || ownerMind.UserId is not { } ownerId
               || !_player.TryGetSessionById(ownerId, out _);
    }

    private List<string> LockedPieces(FSEngineerOnlyRCDComponent comp)
    {
        var locked = new List<string>();
        foreach (var piece in comp.Palette)
        {
            if (_proto.TryIndex<FSBuildRecipePrototype>(piece.Id, out var recipe)
                && recipe.Research is { } node
                && !_research.Has(node))
                locked.Add(piece.Id);
        }

        return locked;
    }

    private int CapFor(int max, EntityUid user)
    {
        return _engineering.IsChiefEngineer(user) ? (int) MathF.Ceiling(max * ChiefCapMultiplier) : max;
    }

    private Dictionary<string, int> CapsFor(FSEngineerOnlyRCDComponent comp, EntityUid user)
    {
        var caps = new Dictionary<string, int>();
        foreach (var (group, max) in comp.Caps)
            caps[group] = CapFor(max, user);

        return caps;
    }

    private static bool DictionaryEquals(Dictionary<string, int> a, Dictionary<string, int> b)
    {
        if (a.Count != b.Count)
            return false;

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var other) || other != value)
                return false;
        }

        return true;
    }

    private void RefundDeconstruct(EntityUid target)
    {
        var coords = Transform(target).Coordinates;
        foreach (var (material, amount) in RefundFor(target) ?? [])
            _stack.SpawnMultipleAtPosition(_proto.Index(material).Spawn, amount, coords);
    }

    public Dictionary<ProtoId<StackPrototype>, int>? RefundFor(EntityUid target)
    {
        var protoId = CompOrNull<MetaDataComponent>(target)?.EntityPrototype?.ID;
        if (protoId == null || !_proto.TryIndex<FSBuildRecipePrototype>(protoId, out var recipe))
            return null;

        var health = 1f;
        if (_destructible.TryGetDestroyedAt(target, out var destroyedAt) && destroyedAt > 0
            && TryComp<DamageableComponent>(target, out var damageable))
            health = Math.Clamp(1f - (damageable.TotalDamage / destroyedAt.Value).Float(), 0f, 1f);

        var fraction = _research.Has(FSEngineeringNodes.SalvagePlating) ? SalvageRefundFraction : RefundFraction;
        var refund = new Dictionary<ProtoId<StackPrototype>, int>();
        foreach (var (material, count) in recipe.Materials)
        {
            var amount = (int) MathF.Floor(count * health * fraction);
            if (amount > 0)
                refund[material] = amount;
        }

        return refund;
    }

}
