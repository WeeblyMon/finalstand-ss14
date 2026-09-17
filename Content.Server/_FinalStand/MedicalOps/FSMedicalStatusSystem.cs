using Content.Server.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSMedicalStatusSystem : EntitySystem
{
    [Dependency] private FSMedicalRosterSystem _roster = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);
    private TimeSpan _nextRefresh;

    private readonly Dictionary<NetUserId, (EntityUid Mob, bool IsMedical)> _lastSent = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev) => _lastSent.Clear();

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
            if (_lastSent.TryGetValue(session.UserId, out var prior)
                && prior.Mob == mob
                && prior.IsMedical == isMedical)
            {
                continue;
            }

            _lastSent[session.UserId] = (mob, isMedical);
            RaiseNetworkEvent(new FSMedicalStatusEvent(isMedical, IsChemist(mob)), session);
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var isMedical = IsMedicalDepartment(ev.Mob);
        _lastSent[ev.Player.UserId] = (ev.Mob, isMedical);
        RaiseNetworkEvent(new FSMedicalStatusEvent(isMedical, IsChemist(ev.Mob)), ev.Player);
    }

    public bool IsMedicalDepartment(EntityUid mob) => _roster.IsMedical(mob);

    private bool IsChemist(EntityUid mob) => _roster.IsChemist(mob);
}
