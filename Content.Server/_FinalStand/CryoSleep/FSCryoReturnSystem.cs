using Content.Shared._FinalStand.CryoSleep;
using Content.Shared.Administration.Logs;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.CCVar;
using Content.Shared.Climbing.Systems;
using Content.Shared.Database;
using Content.Shared.Ghost.Components;
using Content.Shared.Mind;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.CryoSleep;

// returns a cryosleeping player to their own body without a reconnect
public sealed partial class FSCryoReturnSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ISharedAdminLogManager _adminLog = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ClimbSystem _climb = default!;
    [Dependency] private FSCryoSleepSystem _cryo = default!;

    private bool _rejoinEnabled;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.GameCryoSleepRejoining, value => _rejoinEnabled = value, true);
        SubscribeNetworkEvent<FSCryoWakeupRequestEvent>(OnWakeupRequest);
    }

    private void OnWakeupRequest(FSCryoWakeupRequestEvent ev, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        RaiseNetworkEvent(new FSCryoWakeupResponseEvent(TryReturnToBody(session)), session);
    }

    private FSReturnToBodyStatus TryReturnToBody(ICommonSession session)
    {
        if (!_rejoinEnabled)
            return FSReturnToBodyStatus.Disabled;

        if (session.AttachedEntity is not { } ghost || !HasComp<GhostComponent>(ghost))
            return FSReturnToBodyStatus.NotAGhost;

        if (!_cryo.TryGetStoredBody(session.UserId, out var body, out var preferredPod))
            return FSReturnToBodyStatus.BodyMissing;

        if (!TryGetPod(preferredPod, out var pod, out var container))
            return FSReturnToBodyStatus.NoCryopodAvailable;

        var podXform = Transform(pod);
        _transform.SetParent(body, podXform.ParentUid);
        _transform.SetCoordinates(body, podXform.Coordinates);

        if (!_container.Insert(body, container, podXform))
            _climb.ForciblySetClimbing(body, pod);

        if (TryComp<CryostorageComponent>(preferredPod, out var storage))
            storage.StoredPlayers.Remove(body);

        _cryo.Forget(session.UserId);
        _mind.ControlMob(session.UserId, body);

        // without this, vanilla's zero-length grace sweep re-cryos the body immediately
        RemComp<CryostorageContainedComponent>(body);

        _adminLog.Add(LogType.Action, LogImpact.High,
            $"{session.Name} returned from cryosleep into {ToPrettyString(body)}");

        return FSReturnToBodyStatus.Success;
    }

    private bool TryGetPod(EntityUid preferred, out EntityUid pod, out BaseContainer container)
    {
        if (IsPodFree(preferred, out var preferredContainer))
        {
            pod = preferred;
            container = preferredContainer;
            return true;
        }

        var query = EntityQueryEnumerator<FSCryoSleepFallbackComponent, CryostorageComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (uid == preferred || !IsPodFree(uid, out var fallbackContainer))
                continue;

            pod = uid;
            container = fallbackContainer;
            return true;
        }

        pod = default;
        container = default!;
        return false;
    }

    private bool IsPodFree(EntityUid pod, out BaseContainer container)
    {
        container = default!;

        if (TerminatingOrDeleted(pod) || !TryComp<CryostorageComponent>(pod, out var comp))
            return false;

        if (!_container.TryGetContainer(pod, comp.ContainerId, out var found) || found.ContainedEntities.Count > 0)
            return false;

        container = found;
        return true;
    }
}
