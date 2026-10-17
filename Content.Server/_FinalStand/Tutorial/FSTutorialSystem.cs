using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Tutorial;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Tutorial;

public sealed partial class FSTutorialSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private FSPlayerDataStore _store = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawned);
        SubscribeNetworkEvent<FSTutorialSeenEvent>(OnSeen);
        SubscribeNetworkEvent<FSTutorialRequestEvent>(OnRequest);
    }

    private void OnSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.JobId is not { } job
            || TutorialFor(job) is not { } tutorial
            || _store.HasSeenTutorial(ev.Player.UserId.UserId, tutorial.ID))
            return;

        RaiseNetworkEvent(new FSTutorialShowEvent(tutorial.ID), ev.Player);
    }

    private void OnSeen(FSTutorialSeenEvent ev, EntitySessionEventArgs args)
    {
        if (_proto.HasIndex<FSTutorialPrototype>(ev.Tutorial))
            _store.MarkTutorialSeen(args.SenderSession.UserId.UserId, ev.Tutorial);
    }

    private void OnRequest(FSTutorialRequestEvent ev, EntitySessionEventArgs args)
    {
        if (!_mind.TryGetMind(args.SenderSession, out var mindId, out _)
            || !_jobs.MindTryGetJobId(mindId, out var job)
            || job is not { } jobId
            || TutorialFor(jobId) is not { } tutorial)
            return;

        RaiseNetworkEvent(new FSTutorialShowEvent(tutorial.ID), args.SenderSession);
    }

    private FSTutorialPrototype? TutorialFor(string job)
    {
        foreach (var tutorial in _proto.EnumeratePrototypes<FSTutorialPrototype>())
        {
            if (tutorial.Jobs.Contains(job))
                return tutorial;
        }

        return null;
    }
}
