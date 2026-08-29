using System.Linq;
using Content.Server._FinalStand.Leveling;
using Content.Shared._FinalStand.Leaderboard;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Leaderboard;

// Snapshots go only to clients with the window open, and only when a number actually moved.
public sealed class FSLeaderboardSystem : EntitySystem
{
    [Dependency] private FSLevelingSystem _leveling = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(1);

    private readonly HashSet<ICommonSession> _watchers = new();
    private FSLeaderboardEntry[] _lastSent = Array.Empty<FSLeaderboardEntry>();
    private TimeSpan _nextBroadcast;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSLeaderboardWatchEvent>(OnWatch);
    }

    private void OnWatch(FSLeaderboardWatchEvent ev, EntitySessionEventArgs args)
    {
        if (!ev.Watching)
        {
            _watchers.Remove(args.SenderSession);
            return;
        }

        if (!_watchers.Add(args.SenderSession))
            return;

        // Answer immediately, otherwise the window sits empty until the next interval.
        _lastSent = _leveling.GetLeaderboardSnapshot();
        RaiseNetworkEvent(new FSLeaderboardUpdateEvent(_lastSent), args.SenderSession);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_watchers.Count == 0 || _timing.CurTime < _nextBroadcast)
            return;

        _nextBroadcast = _timing.CurTime + BroadcastInterval;

        // A client that dropped or went back to the lobby never sends the closing event.
        _watchers.RemoveWhere(watcher => watcher.Status != SessionStatus.InGame);
        if (_watchers.Count == 0)
            return;

        var entries = _leveling.GetLeaderboardSnapshot();
        if (entries.SequenceEqual(_lastSent))
            return;

        _lastSent = entries;
        RaiseNetworkEvent(new FSLeaderboardUpdateEvent(entries), Filter.Empty().AddPlayers(_watchers));
    }
}
