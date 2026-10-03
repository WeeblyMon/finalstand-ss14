// The RCD's second material source: silos on the same grid within reach. Inventory is spent first; the silo's
// share of a build is reserved when the build starts so two engineers can't both spend the same stock.
using Content.Server.Administration.Logs;
using Content.Shared._FinalStand.RCD;
using Content.Shared.Database;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.RCD;

public sealed partial class FSRCDEngineerOnlySystem
{
    [Dependency] private SharedMaterialStorageSystem _storage = default!;
    [Dependency] private IAdminLogManager _adminLog = default!;

    private const float SiloRange = 20f;

    private readonly Dictionary<ProtoId<StackPrototype>, (string Material, int PerSheet)?> _sheetMaterials = new();
    private readonly Dictionary<EntityUid, Dictionary<string, int>> _reservedVolume = new();
    private readonly Dictionary<EntityUid, List<(EntityUid Silo, string Material, int Volume)>> _rcdReservations = new();

    private IEnumerable<EntityUid> SilosInReach(EntityUid user)
    {
        var userXform = Transform(user);
        var query = EntityQueryEnumerator<OreSiloComponent, MaterialStorageComponent, TransformComponent>();
        while (query.MoveNext(out var silo, out _, out _, out var xform))
        {
            if (xform.GridUid == userXform.GridUid
                && _transform.InRange(xform.Coordinates, userXform.Coordinates, SiloRange))
                yield return silo;
        }
    }

    private (string Material, int PerSheet)? SheetMaterial(ProtoId<StackPrototype> stack)
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

    // Whole sheets of this material the user could draw from silos in reach, minus what other builds have reserved.
    public int SiloSheets(EntityUid user, ProtoId<StackPrototype> stack)
    {
        if (SheetMaterial(stack) is not { } sheet || sheet.PerSheet <= 0)
            return 0;

        var volume = 0;
        foreach (var silo in SilosInReach(user))
            volume += FreeVolume(silo, sheet.Material);

        return volume / sheet.PerSheet;
    }

    private int FreeVolume(EntityUid silo, string material)
    {
        var reserved = _reservedVolume.TryGetValue(silo, out var bySilo) ? bySilo.GetValueOrDefault(material) : 0;
        return Math.Max(0, _storage.GetMaterialAmount(silo, material) - reserved);
    }

    public int Available(EntityUid user, ProtoId<StackPrototype> stack)
    {
        return _materials.Count(user, stack) + SiloSheets(user, stack);
    }

    // Reserves the part of a recipe the user's inventory can't cover. False if the silos can't cover it either.
    private bool TryReserve(EntityUid rcd, EntityUid user, FSBuildRecipePrototype recipe)
    {
        Release(rcd);
        var reservations = new List<(EntityUid Silo, string Material, int Volume)>();

        foreach (var (stack, need) in recipe.Materials)
        {
            var shortSheets = need - _materials.Count(user, stack);
            if (shortSheets <= 0)
                continue;

            if (SheetMaterial(stack) is not { } sheet)
                return Abandon(reservations);

            var volume = shortSheets * sheet.PerSheet;
            foreach (var silo in SilosInReach(user))
            {
                var take = Math.Min(volume, FreeVolume(silo, sheet.Material));
                if (take <= 0)
                    continue;

                Reserve(silo, sheet.Material, take);
                reservations.Add((silo, sheet.Material, take));
                volume -= take;
                if (volume == 0)
                    break;
            }

            if (volume > 0)
                return Abandon(reservations);
        }

        if (reservations.Count > 0)
            _rcdReservations[rcd] = reservations;

        return true;
    }

    private bool Abandon(List<(EntityUid Silo, string Material, int Volume)> reservations)
    {
        foreach (var (silo, material, volume) in reservations)
            Reserve(silo, material, -volume);

        return false;
    }

    private void Reserve(EntityUid silo, string material, int volume)
    {
        if (!_reservedVolume.TryGetValue(silo, out var bySilo))
            _reservedVolume[silo] = bySilo = new Dictionary<string, int>();

        var total = bySilo.GetValueOrDefault(material) + volume;
        if (total <= 0)
            bySilo.Remove(material);
        else
            bySilo[material] = total;
    }

    private void Release(EntityUid rcd)
    {
        if (_rcdReservations.Remove(rcd, out var reservations))
            Abandon(reservations);
    }

    // Spends inventory first, then the reserved silo stock. Releases the reservation either way.
    private bool TryCommit(EntityUid rcd, EntityUid user, FSBuildRecipePrototype recipe)
    {
        var reservations = _rcdReservations.GetValueOrDefault(rcd) ?? [];
        Release(rcd);

        var fromSilo = new List<(EntityUid Silo, string Material, int Volume)>();
        foreach (var (stack, need) in recipe.Materials)
        {
            var shortSheets = need - _materials.Count(user, stack);
            if (shortSheets <= 0)
                continue;

            if (SheetMaterial(stack) is not { } sheet)
                return false;

            var volume = shortSheets * sheet.PerSheet;
            foreach (var (silo, material, reserved) in reservations)
            {
                if (material != sheet.Material || volume == 0)
                    continue;

                var take = Math.Min(volume, Math.Min(reserved, _storage.GetMaterialAmount(silo, material)));
                fromSilo.Add((silo, material, take));
                volume -= take;
            }

            foreach (var silo in SilosInReach(user))
            {
                if (volume == 0)
                    break;

                var take = Math.Min(volume, FreeVolume(silo, sheet.Material) - Planned(fromSilo, silo, sheet.Material));
                if (take <= 0)
                    continue;

                fromSilo.Add((silo, sheet.Material, take));
                volume -= take;
            }

            if (volume > 0)
                return false;
        }

        var inventory = new Dictionary<ProtoId<StackPrototype>, int>();
        foreach (var (stack, need) in recipe.Materials)
            inventory[stack] = Math.Min(need, _materials.Count(user, stack));

        if (!_materials.TryConsume(user, inventory))
            return false;

        foreach (var (silo, material, volume) in fromSilo)
        {
            _storage.TryChangeMaterialAmount(silo, material, -volume);
            _adminLog.Add(LogType.RCD, LogImpact.Low,
                $"{ToPrettyString(user):user} drew {volume} {material} from silo {ToPrettyString(silo)} for an RCD build");
        }

        return true;
    }

    private static int Planned(List<(EntityUid Silo, string Material, int Volume)> plan, EntityUid silo, string material)
    {
        var total = 0;
        foreach (var (s, m, v) in plan)
        {
            if (s == silo && m == material)
                total += v;
        }

        return total;
    }

    // What the RCD's holder could draw from silos, per material in the palette, for the menus and HUD.
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
