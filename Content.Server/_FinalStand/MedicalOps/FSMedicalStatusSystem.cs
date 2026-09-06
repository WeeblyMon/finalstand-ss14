using Content.Server.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSMedicalStatusSystem : EntitySystem
{
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<DepartmentPrototype> MedicalDepartment = "Medical";

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _nextRefresh;

    private readonly Dictionary<EntityUid, bool> _lastSent = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;
        _nextRefresh = _timing.CurTime + RefreshInterval;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } mob)
                continue;

            var isMedical = IsMedicalDepartment(mob);
            if (_lastSent.TryGetValue(mob, out var prior) && prior == isMedical)
                continue;

            _lastSent[mob] = isMedical;
            RaiseNetworkEvent(new FSMedicalStatusEvent(isMedical), session);
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var isMedical = IsMedicalDepartment(ev.Mob);
        _lastSent[ev.Mob] = isMedical;
        RaiseNetworkEvent(new FSMedicalStatusEvent(isMedical), ev.Player);
    }

    public bool IsMedicalDepartment(EntityUid mob)
    {
        return _mind.TryGetMind(mob, out var mindId, out _)
               && _jobs.MindTryGetJob(mindId, out var job)
               && _jobs.TryGetPrimaryDepartment(job.ID, out var department)
               && department.ID == MedicalDepartment;
    }
}
