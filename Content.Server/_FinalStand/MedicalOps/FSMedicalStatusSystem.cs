using Content.Server.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

// Tells each client whether it should render the medic-grade health bars.
public sealed partial class FSMedicalStatusSystem : EntitySystem
{
    [Dependency] private FSMedicalFundSystem _fund = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IGameTiming _timing = default!;

    // Access comes off the held ID, so someone picking up a medical ID mid-round should start
    // seeing the detailed bars without a reconnect.
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

            var isMedical = _fund.IsMedical(mob);
            if (_lastSent.TryGetValue(mob, out var prior) && prior == isMedical)
                continue;

            _lastSent[mob] = isMedical;
            RaiseNetworkEvent(new FSMedicalStatusEvent(isMedical), session);
        }
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var isMedical = _fund.IsMedical(ev.Mob);
        _lastSent[ev.Mob] = isMedical;
        RaiseNetworkEvent(new FSMedicalStatusEvent(isMedical), ev.Player);
    }
}
