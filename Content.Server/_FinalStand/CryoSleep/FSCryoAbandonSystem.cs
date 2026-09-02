using Content.Server.GameTicking;
using Content.Shared._FinalStand.CryoSleep;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;

namespace Content.Server._FinalStand.CryoSleep;

// gives up the cryosleeping body and returns the player to the lobby; wipes the mind
public sealed partial class FSCryoAbandonSystem : EntitySystem
{
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private FSCryoSleepSystem _cryo = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSCryoReturnToLobbyEvent>(OnReturnToLobby);
    }

    private void OnReturnToLobby(FSCryoReturnToLobbyEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (session.AttachedEntity is not { } ghost || !HasComp<GhostComponent>(ghost))
            return;

        if (!_cryo.TryGetStoredBody(session.UserId, out var body, out _))
            return;

        _cryo.Forget(session.UserId);
        QueueDel(body);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{session.Name} abandoned their cryosleeping body and returned to the lobby");

        _gameTicker.Respawn(session);
    }
}
