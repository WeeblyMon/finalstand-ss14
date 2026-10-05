// Stocks every silo at the first prep and tops it up at each one after, so a bad run of drops can't stall building.
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;

namespace Content.Server._FinalStand.Engineering;

public sealed partial class FSSiloSupplySystem : EntitySystem
{
    [Dependency] private SharedMaterialStorageSystem _storage = default!;

    private const int UnitsPerSheet = 100;
    private static readonly (string Material, int Sheets)[] Opening = [("Steel", 40), ("Plasteel", 10)];
    private static readonly (string Material, int Sheets)[] PerWave = [("Steel", 10), ("Plasteel", 2)];

    private bool _stocked;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _stocked = false);
    }

    private void OnPrepStarted(WavePrepStartedEvent ev)
    {
        var supply = _stocked ? PerWave : Opening;
        _stocked = true;

        var query = EntityQueryEnumerator<OreSiloComponent, MaterialStorageComponent>();
        while (query.MoveNext(out var uid, out _, out var storage))
        {
            foreach (var (material, sheets) in supply)
                _storage.TryChangeMaterialAmount(uid, material, sheets * UnitsPerSheet, storage);
        }
    }
}
