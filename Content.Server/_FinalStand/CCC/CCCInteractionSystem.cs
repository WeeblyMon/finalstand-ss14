using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.ReadyCheck;
using Content.Server.Chat.Managers;
using Content.Shared._FinalStand.CCC;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.CCC;

public sealed partial class CCCInteractionSystem : EntitySystem
{
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private ReadyCheckSystem _readyCheck = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int BroadcastMaxLength = 500;
    private static readonly TimeSpan BroadcastCooldown = TimeSpan.FromSeconds(5);

    private float _stateTimer;
    private readonly HashSet<EntityUid> _openActors = new();
    private readonly Dictionary<EntityUid, TimeSpan> _lastBroadcastTime = new();
    private Predicate<EntityUid> _actorGone = default!;

    public override void Initialize()
    {
        base.Initialize();
        _actorGone = actor => !Exists(actor);

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
        _lastBroadcastTime.Remove(args.Actor);
    }

    private void SendCanStartWave(EntityUid actor)
    {
        var jobId = GetJobId(actor);
        var canStart = !_readyCheck.IsCombatPhase()
            && jobId != null
            && ReadyCheckDepts.IsCaptain(jobId);
        if (!TryComp<ActorComponent>(actor, out var actorComp)) return;
        RaiseNetworkEvent(new CCCCanStartWaveEvent(canStart), actorComp.PlayerSession);
    }

    private void BroadcastCanStartWave()
    {
        _openActors.RemoveWhere(_actorGone);

        foreach (var actor in _openActors)
            SendCanStartWave(actor);
    }

    private void OnPrepStarted(WavePrepStartedEvent ev) { PushCCCState(); BroadcastCanStartWave(); _lastBroadcastTime.Clear(); }
    private void OnCombatStarted(WaveCombatStartedEvent ev) { PushCCCState(); BroadcastCanStartWave(); }
    private void OnReadyCheckUpdated(ReadyCheckUpdatedEvent ev) { PushCCCState(); BroadcastCanStartWave(); }

    private void OnStartWave(EntityUid uid, FinalStandCCCComponent comp, CCCStartWaveMessage args)
    {
        if (_readyCheck.IsCombatPhase()) return;
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

        var jobId = GetJobId(args.Actor);
        if (jobId == null || !ReadyCheckDepts.IsCommandJob(jobId)) return;

        var now = _timing.CurTime;
        if (_lastBroadcastTime.TryGetValue(args.Actor, out var last) && now - last < BroadcastCooldown)
            return;
        _lastBroadcastTime[args.Actor] = now;

        var text = args.Text.Length > BroadcastMaxLength ? args.Text[..BroadcastMaxLength] : args.Text;
        _chatManager.DispatchServerAnnouncement($"[COMMAND] {text}", Color.FromHex("#66CCFF"));
    }

    private void PushCCCState()
    {
        if (_openActors.Count == 0)
            return;

        var q = EntityQueryEnumerator<FinalStandCCCComponent>();
        while (q.MoveNext(out var uid, out _))
            PushCCCStateTo(uid);
    }

    private void PushCCCStateTo(EntityUid cccUid)
    {
        if (!_waveRule.TryGetActiveState(out var wave))
            return;

        var cccDmg = TryComp<DamageableComponent>(cccUid, out var dmgComp)
            ? (int)_damageable.GetTotalDamage((cccUid, dmgComp)).Float()
            : 0;

        var cccMax = TryComp<FinalStandCCCTagComponent>(cccUid, out var tag) ? (int)tag.MaxHealth : 0;

        var state = new CCCBoundUserInterfaceState(
            waveNumber: wave.WaveNumber,
            estimatedEnemyCount: wave.TotalEnemies,
            factionDisplay: wave.FactionDisplay,
            isBossWave: wave.IsBossWave,
            waveModifier: wave.IsDarkWave ? "DARK WAVE" : wave.IsBossWave ? "BOSS WAVE" : "Normal",
            currentPhase: wave.Phase,
            secondsToPhaseEnd: wave.SecondsLeft,
            aliveEnemyCount: wave.AliveEnemies,
            activeSpawnerDirections: wave.SpawnerDirections,
            readiedPlayerCount: _readyCheck.GetReadyCount(),
            totalPlayerCount: _readyCheck.GetTotalCount(),
            nextWaveEnemyTypes: wave.NextWaveEnemyTypes,
            cccCurrentDamage: cccDmg,
            cccMaxHealth: cccMax,
            isDarkWave: wave.IsDarkWave);

        _ui.SetUiState(cccUid, CCCUiKey.Key, state);
    }
}
