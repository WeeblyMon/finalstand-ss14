using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Respawn;
using Content.Shared.Administration.Systems;
using Content.Shared.Buckle;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Respawn;

// Opt-in paid respawn during prep. Waiting for a medic stays free; nothing heals free at wave end.
public sealed partial class FSWaveRespawnSystem : EntitySystem
{
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private PullingSystem _pulling = default!;
    [Dependency] private RejuvenateSystem _rejuvenate = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedBuckleSystem _buckle = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSMedicalStatsSystem _medStats = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float CostFraction = 0.20f;
    private const int MinimumCost = 250;
    private const double RequestCooldownSeconds = 2.0;

    private readonly Dictionary<NetUserId, TimeSpan> _lastRequest = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<MindContainerComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeNetworkEvent<FSRespawnRequestMessage>(OnRespawnRequest);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (_mind.TryGetMind(ev.Entity, out _, out var mind) && mind.UserId != null)
            _lastRequest.Remove(mind.UserId.Value);
    }

    private bool IsPrep() => _waveRule.GetPrepComponent() != null;

    private void OnPrepStarted(WavePrepStartedEvent ev) => PushOfferToAll();

    private void OnCombatStarted(WaveCombatStartedEvent ev)
        => RaiseNetworkEvent(new FSRespawnOfferEvent(false, 0), Filter.Broadcast());

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev) => PushOffer(ev.Player);

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev) => _lastRequest.Clear();

    private void OnMobStateChanged(EntityUid uid, MindContainerComponent mindContainer, ref MobStateChangedEvent args)
    {
        if (!IsPrep())
            return;

        if (!_mind.TryGetMind(uid, out _, out var mind) || mind.UserId == null)
            return;

        if (_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            PushOffer(session);
    }

    private void PushOfferToAll()
    {
        foreach (var session in _playerManager.Sessions)
            PushOffer(session);
    }

    private void PushOffer(ICommonSession session)
    {
        if (!IsPrep() || !TryGetRespawnTarget(session, out var mindId, out _))
        {
            RaiseNetworkEvent(new FSRespawnOfferEvent(false, 0), session);
            return;
        }

        RaiseNetworkEvent(new FSRespawnOfferEvent(true, GetCost(mindId)), session);
    }

    // OwnedEntity, not AttachedEntity: a ghosting player is attached to the ghost, not the corpse.
    private bool TryGetRespawnTarget(ICommonSession session, out EntityUid mindId, out EntityUid body)
    {
        mindId = default;
        body = default;

        if (!_mind.TryGetMind(session, out mindId, out var mind))
            return false;

        if (mind.OwnedEntity is not { } owned || !Exists(owned))
            return false;

        if (!HasComp<MobStateComponent>(owned) || !_mobState.IsIncapacitated(owned))
            return false;

        body = owned;
        return true;
    }

    private int GetCost(EntityUid mindId)
        => Math.Max((int)(_wallet.GetCredits(mindId) * CostFraction), MinimumCost);

    private List<EntityCoordinates> CollectPoints()
    {
        var points = new List<EntityCoordinates>();
        var query = EntityQueryEnumerator<FSRespawnPointComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var xform))
            points.Add(xform.Coordinates);
        return points;
    }

    private void OnRespawnRequest(FSRespawnRequestMessage msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;

        if (_lastRequest.TryGetValue(session.UserId, out var last)
            && (_timing.CurTime - last).TotalSeconds < RequestCooldownSeconds)
            return;
        _lastRequest[session.UserId] = _timing.CurTime;

        if (!IsPrep() || !TryGetRespawnTarget(session, out var mindId, out var body))
        {
            PushOffer(session);
            return;
        }

        // Before charging — never take credits for a respawn that cannot happen.
        var points = CollectPoints();
        if (points.Count == 0)
        {
            Log.Error("[FSRespawn] No FSRespawnPoint markers on the map — respawn is impossible.");
            return;
        }

        var cost = GetCost(mindId);
        var charged = _wallet.DeductUpTo(mindId, cost);

        // The pull joint survives a teleport and yanks the puller across the map with the body.
        if (TryComp<PullableComponent>(body, out var pullable))
            _pulling.TryStopPull(body, pullable);

        // A corpse may be inside a body bag or cryo pod, or strapped to a bed.
        _container.TryRemoveFromContainer(body, force: true);
        _buckle.TryUnbuckle(body, body, popup: false);

        _transform.SetCoordinates(body, _random.Pick(points));

        // Before the rejuvenate, or the scoreboard reads this free heal as a revive.
        _medStats.ResetPatient(body);

        // Heal before reattaching, so no frame of the crit screen renders.
        _rejuvenate.PerformRejuvenate(body);
        _mind.ControlMob(session.UserId, body);

        _popup.PopupEntity(Loc.GetString("fs-respawn-charged", ("cost", charged)), body, session);
        PushOffer(session);

        Log.Info($"[FSRespawn] {session.Name} respawned for {charged} credits (quoted {cost}).");
    }
}
