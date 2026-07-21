using Content.Server.Administration.Managers;
using Content.Shared._FinalStand.Lobby;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Lobby;

public sealed class FSPlayerRosterSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        _playerManager.PlayerStatusChanged += OnPlayerStatusChanged;
        SubscribeNetworkEvent<FSPlayerRosterRequestMessage>(OnRosterRequest);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _playerManager.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        BroadcastRoster();
    }

    // Covers sessions that connected before this system's Initialize ran (e.g. the host),
    // which otherwise never see a PlayerStatusChanged after we start listening.
    private void OnRosterRequest(FSPlayerRosterRequestMessage msg, EntitySessionEventArgs args)
    {
        RaiseNetworkEvent(BuildRosterEvent(), args.SenderSession);
    }

    private void BroadcastRoster()
    {
        RaiseNetworkEvent(BuildRosterEvent(), Filter.Broadcast());
    }

    private FSPlayerRosterUpdatedEvent BuildRosterEvent()
    {
        var maxPlayers = _cfg.GetCVar(CCVars.SoftMaxPlayers);
        var players = new List<FSPlayerRosterEntry>();

        foreach (var session in _playerManager.Sessions)
            players.Add(new FSPlayerRosterEntry(session.Name, _adminManager.IsAdmin(session)));

        return new FSPlayerRosterUpdatedEvent(players, maxPlayers);
    }
}
