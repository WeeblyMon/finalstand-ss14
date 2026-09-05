using Content.Server._FinalStand.Engineering;
using Content.Server._FinalStand.Science;
using Content.Shared._FinalStand.Engineering;
using Content.Shared._FinalStand.Science;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Departments;

// Pushes department status to clients; re-checked on a timer since promotions rewrite an ID without firing an event.
public sealed class FSDepartmentStatusSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSScienceOnlySystem _science = default!;
    [Dependency] private FSEngineeringOnlySystem _engineering = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<NetUserId, (bool Science, bool Engineering)> _lastSent = new();
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _lastSent.Clear();
        _nextCheck = TimeSpan.Zero;
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        Send(ev.Player, _science.IsScience(ev.Mob), _engineering.IsEngineering(ev.Mob));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } mob)
                continue;

            var science = _science.IsScience(mob);
            var engineering = _engineering.IsEngineering(mob);

            if (_lastSent.TryGetValue(session.UserId, out var last)
                && last.Science == science
                && last.Engineering == engineering)
            {
                continue;
            }

            Send(session, science, engineering);
        }
    }

    private void Send(ICommonSession session, bool science, bool engineering)
    {
        _lastSent[session.UserId] = (science, engineering);
        var filter = Filter.SinglePlayer(session);
        RaiseNetworkEvent(new FSPlayerScienceStatusEvent(science), filter);
        RaiseNetworkEvent(new FSPlayerEngineeringStatusEvent(engineering), filter);
    }
}
