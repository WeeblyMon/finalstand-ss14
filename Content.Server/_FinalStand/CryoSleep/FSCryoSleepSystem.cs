using Content.Server.Ghost;
using Content.Shared._FinalStand.CryoSleep;
using Content.Shared.Bed.Cryostorage;
using Content.Shared.CCVar;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.CryoSleep;

// entering cryosleep, and the record of who has a body waiting
public sealed class FSCryoSleepSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private GhostSystem _ghost = default!;

    private bool _rejoinEnabled;

    public bool RejoinEnabled => _rejoinEnabled;

    public override void Initialize()
    {
        base.Initialize();

        // Vanilla ships this off; the whole FS cryo flow depends on it, so we own the default without editing theirs.
        _cfg.OverrideDefault(CCVars.GameCryoSleepRejoining, true);
        Subs.CVar(_cfg, CCVars.GameCryoSleepRejoining, value => _rejoinEnabled = value, true);

        SubscribeLocalEvent<CryostorageComponent, InteractHandEvent>(OnPodInteractHand);
        SubscribeLocalEvent<CryostorageComponent, ActivateInWorldEvent>(OnPodActivate);
        SubscribeLocalEvent<CanEnterCryostorageComponent, EntGotInsertedIntoContainerMessage>(OnInsertedIntoPod);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    public bool TryGetStoredBody(NetUserId userId, out EntityUid body, out EntityUid pod)
    {
        body = default;
        pod = default;

        var query = EntityQueryEnumerator<FSCryoStoredBodyComponent>();
        while (query.MoveNext(out var uid, out var stored))
        {
            if (stored.User != userId || TerminatingOrDeleted(uid))
                continue;

            body = uid;
            pod = stored.Pod;
            return true;
        }

        return false;
    }

    public void Forget(NetUserId userId)
    {
        if (TryGetStoredBody(userId, out var body, out _))
            RemComp<FSCryoStoredBodyComponent>(body);
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

        var stored = EnsureComp<FSCryoStoredBodyComponent>(ent);
        stored.User = session.UserId;
        stored.Pod = pod;

        PushStatus(session);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (TryComp<FSCryoStoredBodyComponent>(ev.Entity, out var stored) && stored.User == ev.Player.UserId)
            RemComp<FSCryoStoredBodyComponent>(ev.Entity);

        PushStatus(ev.Player);
    }
}
