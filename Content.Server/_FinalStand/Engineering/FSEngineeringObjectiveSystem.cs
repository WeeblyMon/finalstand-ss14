// Sends engineers a few short "what's next" lines: Mech Bay progress, a low silo, and an emplacement nobody has set up.
using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Emplacement;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.Emplacement;
using Content.Shared._FinalStand.Engineering;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Mech;
using Content.Shared.Materials;
using Content.Shared.Materials.OreSilo;
using System.Linq;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Engineering;

public sealed partial class FSEngineeringObjectiveSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSDepartmentAccessSystem _departments = default!;
    [Dependency] private WaveGameRuleSystem _waves = default!;
    [Dependency] private SharedMaterialStorageSystem _storage = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Resend = TimeSpan.FromSeconds(10);
    private const int LowSteelSheets = 10;
    private const int UnitsPerSheet = 100;

    private TimeSpan _next;
    private TimeSpan _resendAt;
    private List<string> _last = new();

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _next)
            return;

        _next = _timing.CurTime + Interval;
        var lines = Build();
        if (lines.SequenceEqual(_last) && _timing.CurTime < _resendAt)
            return;

        _last = lines;
        _resendAt = _timing.CurTime + Resend;
        var actors = EntityQueryEnumerator<ActorComponent>();
        while (actors.MoveNext(out var uid, out var actor))
        {
            if (_departments.IsEngineering(uid))
                RaiseNetworkEvent(new FSEngineeringObjectiveEvent(lines), actor.PlayerSession);
        }
    }

    private List<string> Build()
    {
        var lines = new List<string>();

        var bays = EntityQueryEnumerator<FSMechBayComponent>();
        while (bays.MoveNext(out var bay))
        {
            if (bay.Ready)
            {
                lines.Add(Loc.GetString("fs-objective-bay-ready"));
                continue;
            }

            if (bay.Installed.Count == 0 || bay.Next() is not { } slot)
                continue;

            lines.Add(Loc.GetString("fs-objective-bay", ("stage", bay.Installed.Count), ("total", bay.Slots.Count),
                ("name", Loc.GetString(slot.Name)),
                ("role", Loc.GetString(slot.Role == FSMechBayRole.Engineering ? "fs-mech-bay-role-engineering" : "fs-mech-bay-role-science"))));
        }

        var silos = 0;
        var steel = 0;
        var query = EntityQueryEnumerator<OreSiloComponent, MaterialStorageComponent>();
        while (query.MoveNext(out var uid, out _, out var storage))
        {
            silos++;
            steel += _storage.GetMaterialAmount(uid, "Steel", storage);
        }

        if (silos > 0 && steel < LowSteelSheets * UnitsPerSheet)
            lines.Add(Loc.GetString("fs-objective-silo-low", ("sheets", steel / UnitsPerSheet)));

        if (_waves.TryGetWaveStatus(out _, out var phase) && KitWaiting())
            lines.Add(Loc.GetString(phase == WavePhase.Combat ? "fs-objective-emplacement-down" : "fs-objective-emplacement-prep"));

        return lines;
    }

    private bool KitWaiting()
    {
        var cores = EntityQueryEnumerator<FSEmplacementComponent>();
        while (cores.MoveNext(out _, out var core))
        {
            if (!core.Packed)
                return false;
        }

        return EntityQueryEnumerator<FSEmplacementKitComponent>().MoveNext(out _, out _);
    }
}
