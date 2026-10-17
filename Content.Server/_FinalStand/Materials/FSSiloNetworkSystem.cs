using Content.Server.Administration.Logs;
using Content.Shared._FinalStand.CCC;
using Content.Shared._FinalStand.Materials;
using Content.Shared._NF.Storage.Components;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Stacks;
using Robust.Server.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Materials;

// All silos share one pool, held by the silo nearest the CCC; the others pass deposits on.
public sealed partial class FSSiloNetworkSystem : EntitySystem
{
    [Dependency] private SharedMaterialStorageSystem _storage = default!;
    [Dependency] private SharedOreSiloSystem _oreSilo = default!;
    [Dependency] private FSSiloReachSystem _reach = default!;
    [Dependency] private FSCarriedMaterialSystem _carried = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private PvsOverrideSystem _pvs = default!;

    private readonly Dictionary<ProtoId<StackPrototype>, (string Material, int PerSheet)?> _sheetMaterials = new();
    private readonly Dictionary<string, int> _reserved = new();
    private EntityUid? _pool;
    private bool _dirty;
    private bool _moving;
    private const float MagnetRange = 1.25f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OreSiloComponent, MapInitEvent>(OnSiloInit);
        SubscribeLocalEvent<OreSiloClientComponent, MapInitEvent>((_, _, _) => _dirty = true);
        SubscribeLocalEvent<FSSiloNetworkComponent, MaterialAmountChangedEvent>(OnAmountChanged);
        SubscribeLocalEvent<FSSiloNetworkComponent, EntityTerminatingEvent>(OnTerminating);
        SubscribeLocalEvent<FSSiloNetworkComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _pool = null;
            _reserved.Clear();
        });
    }

    public EntityUid? Pool
    {
        get
        {
            if (_pool is { } pool && !TerminatingOrDeleted(pool))
                return pool;

            return ChoosePool(null);
        }
    }

    public EntityCoordinates EjectCoordinates(EntityUid storage, EntityUid user)
    {
        var at = Transform(storage).Coordinates;
        if (!HasComp<FSSiloNetworkComponent>(storage))
            return at;

        var best = float.MaxValue;
        var userPos = _transform.GetWorldPosition(user);
        var query = EntityQueryEnumerator<FSSiloNetworkComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
        {
            if (xform.MapID != Transform(user).MapID)
                continue;

            var distance = (_transform.GetWorldPosition(xform) - userPos).LengthSquared();
            if (distance >= best)
                continue;

            best = distance;
            at = xform.Coordinates;
        }

        return at;
    }

    public EntityUid? PoolFor(EntityUid user) => Pool is { } pool && _reach.InReach(user) ? pool : null;

    private void OnSiloInit(Entity<OreSiloComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<FSSiloNetworkComponent>(ent);
        var magnet = EnsureComp<MaterialStorageMagnetPickupComponent>(ent);
        magnet.MagnetEnabled = true;
        magnet.Range = MagnetRange;
        _dirty = true;
    }

    private void OnAmountChanged(Entity<FSSiloNetworkComponent> ent, ref MaterialAmountChangedEvent args)
    {
        if (!_moving && !ent.Comp.Pool && Pool is { } pool)
            MoveAll(ent, pool);
    }

    private void OnTerminating(Entity<FSSiloNetworkComponent> ent, ref EntityTerminatingEvent args)
    {
        if (!ent.Comp.Pool)
            return;

        _pool = null;
        if (ChoosePool(ent) is { } next)
            MoveAll(ent, next);
        _dirty = true;
    }

    private void OnExamined(Entity<FSSiloNetworkComponent> ent, ref ExaminedEvent args)
    {
        if (Pool is not { } pool || !TryComp<MaterialStorageComponent>(pool, out var storage))
            return;

        using (args.PushGroup(nameof(FSSiloNetworkSystem)))
        {
            args.PushMarkup(Loc.GetString(ent.Owner == pool ? "fs-silo-examine-pool" : "fs-silo-examine-dropoff"));
            var empty = true;
            foreach (var (id, amount) in storage.Storage)
            {
                if (!_proto.TryIndex<MaterialPrototype>(id, out var material))
                    continue;

                var sheets = amount / _storage.GetSheetVolume(material);
                if (sheets <= 0)
                    continue;

                empty = false;
                args.PushMarkup(Loc.GetString("fs-silo-examine-line", ("sheets", sheets), ("name", Loc.GetString(material.Name))));
            }

            if (empty)
                args.PushMarkup(Loc.GetString("fs-silo-examine-empty"));
        }
    }

    public override void Update(float frameTime)
    {
        if (!_dirty)
            return;

        _dirty = false;
        if (Pool == null)
            ChoosePool(null);

        if (Pool is not { } pool || !TryComp<OreSiloComponent>(pool, out var poolSilo))
            return;

        var silos = EntityQueryEnumerator<FSSiloNetworkComponent>();
        while (silos.MoveNext(out var uid, out var network))
        {
            if (uid != pool)
                MoveAll(uid, pool);
        }

        var clients = EntityQueryEnumerator<OreSiloClientComponent>();
        while (clients.MoveNext(out var uid, out var client))
            _oreSilo.FSLinkClient((pool, poolSilo), (uid, client));
    }

    private EntityUid? ChoosePool(EntityUid? except)
    {
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        var ccc = CccPosition();
        var query = EntityQueryEnumerator<FSSiloNetworkComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var network, out var xform))
        {
            if (uid == except || TerminatingOrDeleted(uid))
                continue;

            var distance = ccc is { } at && xform.MapID == at.MapId
                ? (_transform.GetWorldPosition(xform) - at.Position).LengthSquared()
                : float.MaxValue / 2f;
            if (best != null && distance >= bestDistance)
                continue;

            best = uid;
            bestDistance = distance;
        }

        var all = EntityQueryEnumerator<FSSiloNetworkComponent>();
        while (all.MoveNext(out var uid, out var network))
        {
            var isPool = uid == best;
            if (network.Pool == isPool)
                continue;

            network.Pool = isPool;
            Dirty(uid, network);
        }

        if (_pool != best)
        {
            if (_pool is { } old && Exists(old))
                _pvs.RemoveGlobalOverride(old);
            if (best is { } chosen)
                _pvs.AddGlobalOverride(chosen);
        }

        _pool = best;
        return best;
    }

    private Robust.Shared.Map.MapCoordinates? CccPosition()
    {
        var query = EntityQueryEnumerator<FinalStandCCCComponent, TransformComponent>();
        return query.MoveNext(out var uid, out _, out _) ? _transform.GetMapCoordinates(uid) : null;
    }

    private void MoveAll(EntityUid from, EntityUid to)
    {
        if (!TryComp<MaterialStorageComponent>(from, out var fromStorage) || fromStorage.Storage.Count == 0)
            return;

        _moving = true;
        var materials = new Dictionary<string, int>();
        foreach (var (material, amount) in fromStorage.Storage)
            materials[material] = amount;

        foreach (var (material, amount) in materials)
        {
            if (_storage.TryChangeMaterialAmount(to, material, amount))
                _storage.TryChangeMaterialAmount(from, material, -amount, fromStorage, localOnly: true);
        }

        _moving = false;
    }

    public (string Material, int PerSheet)? SheetMaterial(ProtoId<StackPrototype> stack)
    {
        if (_sheetMaterials.TryGetValue(stack, out var cached))
            return cached;

        (string, int)? result = null;
        if (_proto.Index(stack).Spawn is { } spawn
            && _proto.Index(spawn).TryGetComponent<PhysicalCompositionComponent>(out var composition, EntityManager.ComponentFactory))
        {
            foreach (var (material, volume) in composition.MaterialComposition)
            {
                result = (material, volume);
                break;
            }
        }

        _sheetMaterials[stack] = result;
        return result;
    }

    public int Free(string material)
    {
        return Pool is { } pool
            ? Math.Max(0, _storage.GetMaterialAmount(pool, material) - _reserved.GetValueOrDefault(material))
            : 0;
    }

    public void Reserve(string material, int volume)
    {
        var total = _reserved.GetValueOrDefault(material) + volume;
        if (total <= 0)
            _reserved.Remove(material);
        else
            _reserved[material] = total;
    }

    public int Sheets(EntityUid user, ProtoId<StackPrototype> stack)
    {
        return PoolFor(user) != null && SheetMaterial(stack) is { PerSheet: > 0 } sheet
            ? Free(sheet.Material) / sheet.PerSheet
            : 0;
    }

    public bool Has(EntityUid user, IReadOnlyDictionary<ProtoId<StackPrototype>, int> materials)
    {
        var carried = _carried.CountAll(user);
        foreach (var (stack, need) in materials)
        {
            if (carried.GetValueOrDefault(stack) + Sheets(user, stack) < need)
                return false;
        }

        return true;
    }

    public Dictionary<ProtoId<StackPrototype>, int> Missing(EntityUid user, IReadOnlyDictionary<ProtoId<StackPrototype>, int> materials)
    {
        var carried = _carried.CountAll(user);
        var missing = new Dictionary<ProtoId<StackPrototype>, int>();
        foreach (var (stack, need) in materials)
        {
            var have = carried.GetValueOrDefault(stack) + Sheets(user, stack);
            if (have < need)
                missing[stack] = need - have;
        }

        return missing;
    }

    public bool TryConsume(EntityUid user, IReadOnlyDictionary<ProtoId<StackPrototype>, int> materials)
    {
        if (!Has(user, materials))
            return false;

        var carried = _carried.CountAll(user);
        var fromHand = new Dictionary<ProtoId<StackPrototype>, int>();
        var fromPool = new Dictionary<string, int>();
        foreach (var (stack, need) in materials)
        {
            var hand = Math.Min(need, carried.GetValueOrDefault(stack));
            if (hand > 0)
                fromHand[stack] = hand;

            if (need - hand > 0 && SheetMaterial(stack) is { } sheet)
                fromPool[sheet.Material] = fromPool.GetValueOrDefault(sheet.Material) + (need - hand) * sheet.PerSheet;
        }

        if (fromHand.Count > 0 && !_carried.TryConsume(user, fromHand))
            return false;

        if (fromPool.Count > 0 && Pool is { } pool)
        {
            foreach (var (material, volume) in fromPool)
                _storage.TryChangeMaterialAmount(pool, material, -volume);
            _adminLog.Add(LogType.RCD, LogImpact.Low,
                $"{ToPrettyString(user):user} drew {string.Join(", ", fromPool)} from the silo network");
        }

        return true;
    }
}
