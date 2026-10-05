using Content.Server._FinalStand.Deployables;
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
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.RCD;

public sealed partial class FSRCDEngineerOnlySystem
{
    [Dependency] private FSPlacementRuleSystem _placementRules = default!;
    [Dependency] private FSDeployableSystem _deployable = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private FSBreachEvaluator _breach = default!;
    [Dependency] private FSCarriedMaterialSystem _materials = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private FSEngineeringResearchSystem _research = default!;

    private const float RefundFraction = 0.5f;
    private const float SalvageRefundFraction = 0.75f;
    private const float RapidAssemblyMultiplier = 0.75f;
    private const float ChiefCapMultiplier = 1.5f;
    private const float ThreatRange = 5f;
    private const float ThreatDelayMultiplier = 3f;

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _threats = new();


    public string? GetBlockReason(EntityUid rcd, FSEngineerOnlyRCDComponent comp, EntityUid user, EntityCoordinates location)
    {
        if (!_engineering.IsEngineering(user))
            return Loc.GetString("fs-rcd-engineers-only");

        if (!TryComp<RCDComponent>(rcd, out var rcdComp))
            return null;

        var rcdProto = _proto.Index(rcdComp.ProtoId);
        if (rcdProto.Mode != RcdMode.ConstructObject)
            return null;

        if (IsNoBuildTile(location, user))
            return Loc.GetString("fs-rcd-build-blocked");

        if (!_proto.TryIndex<FSBuildRecipePrototype>(rcdComp.ProtoId.Id, out var recipe))
            return null;

        Direction? facing = rcdProto.Rotation == RcdRotation.User ? rcdComp.ConstructionDirection : null;

        if (recipe.Research is { } node && !_research.Has(node))
            return Loc.GetString("fs-rcd-needs-research");

        if (!_placementRules.IsTileBuildable(location, blocksPath: true))
            return Loc.GetString("fs-rcd-in-the-way");

        if (_breach.IsAdjacentToSpace(location))
            return Loc.GetString("fs-rcd-next-to-space");

        if (_deployable.IsOccupied(location, facing))
            return Loc.GetString("fs-deployable-occupied");

        var built = StationBuilt();
        if (built >= comp.StationCap)
            return Loc.GetString("fs-rcd-station-cap", ("used", built), ("max", comp.StationCap));

        if (recipe.CapGroup is { } group && comp.Caps.TryGetValue(group, out var baseMax))
        {
            var max = CapFor(baseMax, user);
            var used = CapUsed(user, group);
            if (used >= max)
                return Loc.GetString("fs-rcd-cap",
                    ("group", Loc.GetString("fs-rcd-cap-group-" + group)), ("used", used), ("max", max));
        }

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
                return;
            }
        }
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

        var spent = new List<string>();
        foreach (var (material, amount) in recipe.Materials)
            spent.Add(Loc.GetString("fs-rcd-spent", ("amount", amount), ("name", Loc.GetString(_proto.Index(material).Name))));
        _popup.PopupCoordinates(string.Join("  ", spent), Transform(args.Built).Coordinates, args.User, PopupType.Small);

        var directional = _proto.TryIndex<RCDPrototype>(protoId, out var rcdProto)
            && rcdProto.Rotation == RcdRotation.User;

        if (!Transform(args.Built).Anchored && !HasComp<PullableComponent>(args.Built))
            _transform.AnchorEntity(args.Built);

        var deployedBy = EnsureComp<FSDeployedByComponent>(args.Built);
        deployedBy.OwnerMind = _mind.TryGetMind(args.User, out var mindId, out _) ? mindId : null;
        deployedBy.DeployedBy = args.User;
        deployedBy.SourceProto = protoId;
        deployedBy.Facing = directional ? args.Direction : null;

        RefreshUsage(ent, args.User);
    }

    public void RefreshUsage(Entity<FSEngineerOnlyRCDComponent> rcd, EntityUid user)
    {
        var mind = _mind.TryGetMind(user, out var mindId, out _) ? mindId : (EntityUid?) null;
        var used = new Dictionary<string, int>();
        var station = 0;

        var query = EntityQueryEnumerator<FSDeployedByComponent>();
        while (query.MoveNext(out var uid, out var deployedBy))
        {
            if (TerminatingOrDeleted(uid) || !_proto.TryIndex<FSBuildRecipePrototype>(deployedBy.SourceProto.Id, out var recipe))
                continue;

            station++;
            if (mind != null && deployedBy.OwnerMind == mind && recipe.CapGroup is { } group)
                used[group] = used.GetValueOrDefault(group) + 1;
        }

        var silo = SiloStockFor(user, rcd.Comp);
        var max = CapsFor(rcd.Comp, user);
        if (station == rcd.Comp.StationUsed
            && DictionaryEquals(used, rcd.Comp.CapsUsed)
            && DictionaryEquals(max, rcd.Comp.CapsMax)
            && DictionaryEquals(silo, rcd.Comp.SiloStock))
            return;

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
        var protoId = MetaData(target).EntityPrototype?.ID;
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

    private int CapUsed(EntityUid user, string group)
    {
        if (!_mind.TryGetMind(user, out var mindId, out _))
            return 0;

        var count = 0;
        var query = EntityQueryEnumerator<FSDeployedByComponent>();
        while (query.MoveNext(out var uid, out var deployedBy))
        {
            if (deployedBy.OwnerMind == mindId
                && !TerminatingOrDeleted(uid)
                && _proto.TryIndex<FSBuildRecipePrototype>(deployedBy.SourceProto.Id, out var recipe)
                && recipe.CapGroup == group)
                count++;
        }

        return count;
    }

    private int StationBuilt()
    {
        var count = 0;
        var query = EntityQueryEnumerator<FSDeployedByComponent>();
        while (query.MoveNext(out var uid, out var deployedBy))
        {
            if (!TerminatingOrDeleted(uid) && _proto.HasIndex<FSBuildRecipePrototype>(deployedBy.SourceProto.Id))
                count++;
        }

        return count;
    }
}
