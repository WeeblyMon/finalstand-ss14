using Content.Server._FinalStand.Materials;
using Content.Server.Administration.Logs;
using Content.Shared._FinalStand.RCD;
using Content.Shared.Database;
using Content.Shared.Materials;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.RCD;

// The network's share of a build is reserved at start so two engineers can't spend the same stock.
public sealed partial class FSRCDEngineerOnlySystem
{
    [Dependency] private FSSiloNetworkSystem _silo = default!;
    [Dependency] private SharedMaterialStorageSystem _storage = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    // One entry per build an RCD has started, oldest first: vanilla lets several run at once.
    private readonly Dictionary<EntityUid, Queue<PendingBuild>> _pendingBuilds = new();

    private readonly record struct PendingBuild(TimeSpan StartedAt, List<(string Material, int Volume)> Reserved);

    public int SiloSheets(EntityUid user, ProtoId<StackPrototype> stack) => _silo.Sheets(user, stack);

    public int Available(EntityUid user, ProtoId<StackPrototype> stack)
    {
        return _materials.Count(user, stack) + SiloSheets(user, stack);
    }

    // Reserves the part of a recipe the user's inventory can't cover. False if the network can't cover it either.
    private bool TryReserve(EntityUid rcd, EntityUid user, FSBuildRecipePrototype recipe)
    {
        var reservations = new List<(string Material, int Volume)>();
        foreach (var (stack, need) in recipe.Materials)
        {
            var shortSheets = need - _materials.Count(user, stack);
            if (shortSheets <= 0)
                continue;

            if (_silo.PoolFor(user) == null || _silo.SheetMaterial(stack) is not { } sheet)
                return Abandon(reservations);

            var volume = shortSheets * sheet.PerSheet;
            if (_silo.Free(sheet.Material) < volume)
                return Abandon(reservations);

            _silo.Reserve(sheet.Material, volume);
            reservations.Add((sheet.Material, volume));
        }

        if (!_pendingBuilds.TryGetValue(rcd, out var queue))
            _pendingBuilds[rcd] = queue = new Queue<PendingBuild>();

        queue.Enqueue(new PendingBuild(_timing.CurTime, reservations));
        return true;
    }

    private bool Abandon(List<(string Material, int Volume)> reservations)
    {
        foreach (var (material, volume) in reservations)
            _silo.Reserve(material, -volume);

        return false;
    }

    // Builds on one RCD finish roughly in the order they started, so the oldest share goes back.
    private void ReleaseOldest(EntityUid rcd)
    {
        if (!_pendingBuilds.TryGetValue(rcd, out var queue) || !queue.TryDequeue(out var build))
            return;

        Abandon(build.Reserved);
        if (queue.Count == 0)
            _pendingBuilds.Remove(rcd);
    }

    // A build vanilla refused after we reserved never finishes or cancels, so its share times out.
    private void ExpireReservations()
    {
        _expired.Clear();
        foreach (var (rcd, queue) in _pendingBuilds)
        {
            if (queue.Peek().StartedAt + ReservationTimeout < _timing.CurTime)
                _expired.Add(rcd);
        }

        foreach (var rcd in _expired)
            ReleaseOldest(rcd);
    }

    // Spends inventory first, then the network. Releases the reservation either way.
    private bool TryCommit(EntityUid rcd, EntityUid user, FSBuildRecipePrototype recipe)
    {
        ReleaseOldest(rcd);

        var inventory = new Dictionary<ProtoId<StackPrototype>, int>();
        var fromPool = new Dictionary<string, int>();
        foreach (var (stack, need) in recipe.Materials)
        {
            var carried = Math.Min(need, _materials.Count(user, stack));
            inventory[stack] = carried;
            if (need - carried <= 0)
                continue;

            if (_silo.SheetMaterial(stack) is not { } sheet)
                return false;

            fromPool[sheet.Material] = fromPool.GetValueOrDefault(sheet.Material) + (need - carried) * sheet.PerSheet;
        }

        if (fromPool.Count > 0)
        {
            if (_silo.PoolFor(user) == null)
                return false;

            // Free, not the raw amount: stock other engineers reserved for their builds isn't ours to take.
            foreach (var (material, volume) in fromPool)
            {
                if (_silo.Free(material) < volume)
                    return false;
            }
        }

        if (!_materials.TryConsume(user, inventory))
            return false;

        if (fromPool.Count > 0 && _silo.Pool is { } source)
        {
            foreach (var (material, volume) in fromPool)
            {
                _storage.TryChangeMaterialAmount(source, material, -volume);
                _adminLog.Add(LogType.RCD, LogImpact.Low,
                    $"{ToPrettyString(user):user} drew {volume} {material} from the silo network for an RCD build");
            }
        }

        return true;
    }

    // What the RCD's holder could draw from the network, per material in the palette, for the menus and HUD.
    private Dictionary<string, int> SiloStockFor(EntityUid user, FSEngineerOnlyRCDComponent comp)
    {
        var stock = new Dictionary<string, int>();
        foreach (var piece in comp.Palette)
        {
            if (!_proto.TryIndex<FSBuildRecipePrototype>(piece.Id, out var recipe))
                continue;

            foreach (var stack in recipe.Materials.Keys)
            {
                if (!stock.ContainsKey(stack))
                    stock[stack] = SiloSheets(user, stack);
            }
        }

        return stock;
    }
}
