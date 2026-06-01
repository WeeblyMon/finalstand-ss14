using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.ReadyCheck;
using Content.Server.Chat.Managers;
using Content.Shared._FinalStand.CCC;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Station;

public sealed class CCCInteractionSystem : EntitySystem
{
    [Dependency] private readonly WaveGameRuleSystem _waveRule = default!;
    [Dependency] private readonly ReadyCheckSystem _readyCheck = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;

    private float _stateTimer;
    private readonly HashSet<EntityUid> _openActors = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<ReadyCheckUpdatedEvent>(OnReadyCheckUpdated);

        Subs.BuiEvents<FinalStandCCCComponent>(CCCUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnCCCOpened);
            subs.Event<BoundUIClosedEvent>(OnCCCClosed);
            subs.Event<CCCStartWaveMessage>(OnStartWave);
            subs.Event<CCCBroadcastMessage>(OnBroadcast);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _stateTimer += frameTime;
        if (_stateTimer < 2f) return;
        _stateTimer = 0f;
        PushCCCState();
    }

    private void OnCCCOpened(EntityUid uid, FinalStandCCCComponent comp, BoundUIOpenedEvent args)
    {
        _openActors.Add(args.Actor);
        PushCCCStateTo(uid);
        SendCanStartWave(args.Actor);
    }

    private void OnCCCClosed(EntityUid uid, FinalStandCCCComponent comp, BoundUIClosedEvent args)
    {
        _openActors.Remove(args.Actor);
    }

    private void SendCanStartWave(EntityUid actor)
    {
        var jobId = GetJobId(actor);
        var canStart = !_readyCheck.IsCombatPhase()
            && _readyCheck.ReadyCount() >= 1
            && jobId != null
            && ReadyCheckDepts.IsCaptain(jobId);
        if (!TryComp<ActorComponent>(actor, out var actorComp)) return;
        RaiseNetworkEvent(new CCCCanStartWaveEvent(canStart), actorComp.PlayerSession);
    }

    private void BroadcastCanStartWave()
    {
        foreach (var actor in _openActors.ToList())
        {
            if (!Exists(actor)) { _openActors.Remove(actor); continue; }
            SendCanStartWave(actor);
        }
    }

    private void OnPrepStarted(WavePrepStartedEvent ev) { PushCCCState(); BroadcastCanStartWave(); }
    private void OnCombatStarted(WaveCombatStartedEvent ev) { PushCCCState(); BroadcastCanStartWave(); }
    private void OnReadyCheckUpdated(ReadyCheckUpdatedEvent ev) { PushCCCState(); BroadcastCanStartWave(); }

    private void OnStartWave(EntityUid uid, FinalStandCCCComponent comp, CCCStartWaveMessage args)
    {
        if (_readyCheck.IsCombatPhase()) return;
        if (_readyCheck.ReadyCount() < 1) return;
        var jobId = GetJobId(args.Actor);
        if (jobId == null || !ReadyCheckDepts.IsCaptain(jobId)) return;
        RaiseLocalEvent(new WaveStartRequestEvent());
    }

    private string? GetJobId(EntityUid player)
    {
        if (!_mind.TryGetMind(player, out var mindId, out _))
            return null;
        return _jobs.MindTryGetJob(mindId, out var job) ? job.ID : null;
    }

    private void OnBroadcast(EntityUid uid, FinalStandCCCComponent comp, CCCBroadcastMessage args)
    {
        if (string.IsNullOrWhiteSpace(args.Text)) return;
        _chatManager.DispatchServerAnnouncement($"[COMMAND] {args.Text}", Color.FromHex("#66CCFF"));
    }

    private void PushCCCState()
    {
        var q = EntityQueryEnumerator<FinalStandCCCComponent>();
        while (q.MoveNext(out var uid, out _))
            PushCCCStateTo(uid);
    }

    private void PushCCCStateTo(EntityUid cccUid)
    {
        if (!_waveRule.TryGetActiveState(out var wave))
            return;

        var statuses = _readyCheck.GetStatuses();
        var state = new CCCBoundUserInterfaceState(
            waveNumber: wave.WaveNumber,
            estimatedEnemyCount: wave.TotalEnemies,
            factionDisplay: wave.FactionDisplay,
            isBossWave: wave.IsBossWave,
            waveModifier: wave.IsBossWave ? "BOSS WAVE" : "Normal",
            currentPhase: wave.Phase,
            secondsToPhaseEnd: wave.SecondsLeft,
            aliveEnemyCount: wave.AliveEnemies,
            activeSpawnerCount: wave.SpawnerCount,
            departmentStatus: statuses,
            readyCount: _readyCheck.ReadyCount(),
            nextWaveEnemyTypes: wave.NextWaveEnemyTypes);

        _ui.SetUiState(cccUid, CCCUiKey.Key, state);
    }
}
