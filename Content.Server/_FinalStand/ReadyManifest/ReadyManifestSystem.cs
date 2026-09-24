// Counts which jobs readied-up players have on High, for the pre-round lobby Ready Manifest (ported from Moffstation).
using System.Linq;
using Content.Server.EUI;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Server.Preferences.Managers;
using Content.Shared._FinalStand.ReadyManifest;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.ReadyManifest;

public sealed class ReadyManifestSystem : EntitySystem
{
    [Dependency] private EuiManager _euiManager = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IServerPreferencesManager _prefsManager = default!;
    [Dependency] private IPrototypeManager _protoMan = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private readonly Dictionary<ICommonSession, ReadyManifestEui> _openEuis = [];
    private readonly Dictionary<ProtoId<JobPrototype>, int> _jobCounts = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestReadyManifestMessage>(OnRequestReadyManifest);
        SubscribeLocalEvent<PlayerToggleReadyEvent>(OnPlayerToggleReady);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        // Close() re-enters RemoveEui synchronously, so iterate a copy.
        foreach (var eui in _openEuis.Values.ToList())
            eui.Close();
    }

    private void OnRequestReadyManifest(RequestReadyManifestMessage message, EntitySessionEventArgs args)
    {
        if (_gameTicker.RunLevel != GameRunLevel.PreRoundLobby)
            return;

        BuildReadyManifest();
        OpenEui(args.SenderSession);
    }

    private void OnPlayerToggleReady(ref PlayerToggleReadyEvent ev)
    {
        BuildReadyManifest();

        foreach (var eui in _openEuis.Values)
            eui.StateDirty();
    }

    private void BuildReadyManifest()
    {
        _jobCounts.Clear();

        foreach (var job in _protoMan.EnumeratePrototypes<JobPrototype>())
        {
            if (job.SetPreference)
                _jobCounts.Add(job.ID, 0);
        }

        foreach (var (userId, status) in _gameTicker.PlayerGameStatuses)
        {
            if (status == PlayerGameStatus.ReadyToPlay)
                CountPlayer(userId);
        }
    }

    private void CountPlayer(NetUserId userId)
    {
        // A readied player who has since disconnected won't spawn, so don't advertise their pick.
        if (!_playerManager.TryGetSessionById(userId, out var session) || session.Status == SessionStatus.Disconnected)
            return;

        if (!_prefsManager.TryGetCachedPreferences(userId, out var preferences))
            return;

        foreach (var (job, priority) in preferences.SelectedCharacter.JobPriorities)
        {
            if (priority == JobPriority.High && _jobCounts.ContainsKey(job))
                _jobCounts[job]++;
        }
    }

    public IReadOnlyDictionary<ProtoId<JobPrototype>, int> GetReadyManifest()
    {
        return _jobCounts;
    }

    private void OpenEui(ICommonSession session)
    {
        if (_openEuis.ContainsKey(session))
            return;

        var eui = new ReadyManifestEui(this);
        _openEuis.Add(session, eui);
        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }

    public void RemoveEui(ICommonSession session)
    {
        _openEuis.Remove(session);
    }
}
