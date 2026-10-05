// Stocks every silo at the first prep and tops it up at each one after, and warns engineers when steel runs low.
using Content.Server._FinalStand.Departments;
using Content.Server.Chat.Managers;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.GameTicking;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Engineering;

public sealed partial class FSSiloSupplySystem : EntitySystem
{
    [Dependency] private SharedMaterialStorageSystem _storage = default!;
    [Dependency] private FSDepartmentAccessSystem _departments = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int UnitsPerSheet = 100;
    private static readonly (string Material, int Sheets)[] Opening = [("Steel", 40), ("Plasteel", 10)];
    private static readonly (string Material, int Sheets)[] PerWave = [("Steel", 10), ("Plasteel", 2)];

    private const int LowSteel = 10 * UnitsPerSheet;
    private const int RecoveredSteel = 20 * UnitsPerSheet;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);

    private bool _stocked;
    private bool _warnedLow;
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ =>
        {
            _stocked = false;
            _warnedLow = false;
        });
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

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;
        var steel = 0;
        var silos = 0;
        var query = EntityQueryEnumerator<OreSiloComponent, MaterialStorageComponent>();
        while (query.MoveNext(out var uid, out _, out var storage))
        {
            silos++;
            steel += _storage.GetMaterialAmount(uid, "Steel", storage);
        }

        if (silos == 0)
            return;

        if (_warnedLow && steel >= RecoveredSteel)
            _warnedLow = false;

        if (_warnedLow || steel >= LowSteel)
            return;

        _warnedLow = true;
        var message = Loc.GetString("fs-silo-low", ("sheets", steel / UnitsPerSheet));
        var actors = EntityQueryEnumerator<ActorComponent>();
        while (actors.MoveNext(out var uid, out var actor))
        {
            if (_departments.IsEngineering(uid))
                _chat.DispatchServerMessage(actor.PlayerSession, message);
        }
    }
}
