using Content.Server.Ghost;
using Content.Shared._FinalStand.CryoSleep;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.CryoSleep;

// entering cryosleep, and the record of who has a body waiting
public sealed partial class FSCryoSleepSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private GhostSystem _ghost = default!;

    private readonly Dictionary<NetUserId, StoredBody> _stored = new();

    private bool _rejoinEnabled;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, CCVars.GameCryoSleepRejoining, value => _rejoinEnabled = value, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _stored.Clear());
        SubscribeLocalEvent<CryostorageComponent, InteractHandEvent>(OnPodInteractHand);
        SubscribeLocalEvent<CryostorageComponent, ActivateInWorldEvent>(OnPodActivate);
        SubscribeLocalEvent<CanEnterCryostorageComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoPod);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    public bool TryGetStoredBody(NetUserId userId, out EntityUid body, out EntityUid pod)
    {
        body = default;
        pod = default;

        if (!_stored.TryGetValue(userId, out var stored))
            return false;

        if (TerminatingOrDeleted(stored.Body))
        {
            _stored.Remove(userId);
            return false;
        }

        body = stored.Body;
        pod = stored.Pod;
        return true;
    }

    public void Forget(NetUserId userId)
    {
        _stored.Remove(userId);
    }

    public void PushStatus(ICommonSession session)
    {
        RaiseNetworkEvent(new FSCryoStatusEvent(TryGetStoredBody(session.UserId, out _, out _)), session);
    }

    private void OnPodInteractHand(Entity<CryostorageComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryEnterPod(ent, args.User);
    }

    private void OnPodActivate(Entity<CryostorageComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryEnterPod(ent, args.User);
    }

    private bool TryEnterPod(Entity<CryostorageComponent> pod, EntityUid user)
    {
        if (!_container.TryGetContainer(pod.Owner, pod.Comp.ContainerId, out var container))
            return false;

        if (container.ContainedEntities.Count > 0)
            return false;

        return _container.Insert(user, container);
    }

    private void OnInsertedIntoPod(Entity<CanEnterCryostorageComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!_rejoinEnabled)
            return;

        var pod = args.Container.Owner;
        if (!TryComp<CryostorageComponent>(pod, out var cryostorage) || args.Container.ID != cryostorage.ContainerId)
            return;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        if (!_mind.TryGetMind(ent.Owner, out var mindId, out _))
            return;

        var session = actor.PlayerSession;
        _ghost.OnGhostAttempt(mindId, canReturnGlobal: false, forced: true);

        _stored[session.UserId] = new StoredBody(ent.Owner, pod);
        PushStatus(session);
    }

    // Taking control of the stored body - our own return, or vanilla's reconnect path - ends the
    // stay in cryo. Anything else attaching just refreshes the ghost bar.
    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (_stored.TryGetValue(ev.Player.UserId, out var stored) && stored.Body == ev.Entity)
            _stored.Remove(ev.Player.UserId);

        PushStatus(ev.Player);
    }

    private readonly record struct StoredBody(EntityUid Body, EntityUid Pod);
}
