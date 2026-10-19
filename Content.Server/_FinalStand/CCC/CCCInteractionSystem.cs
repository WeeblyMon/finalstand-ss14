using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.ReadyCheck;
using Content.Server.Chat.Managers;
using Content.Shared._FinalStand.CCC;
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
    [Dependency] private FSReadyUpSystem _readyUp = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _jobs = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const int BroadcastMaxLength = 500;
    private static readonly TimeSpan BroadcastCooldown = TimeSpan.FromSeconds(5);

    // The CCC can be hit many times a second; an open console only needs to see that twice a second.
    private static readonly TimeSpan DamageCheckInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextDamageCheck;
    private readonly Dictionary<EntityUid, int> _shownDamage = new();
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
        if (_openActors.Count == 0 || _timing.CurTime < _nextDamageCheck)
            return;

        _nextDamageCheck = _timing.CurTime + DamageCheckInterval;
        var query = EntityQueryEnumerator<FinalStandCCCComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out _, out var damageable))
        {
            if (_shownDamage.GetValueOrDefault(uid, -1) != GetDamage((uid, damageable)))
                PushCCCStateTo(uid);
        }
    }

    private int GetDamage(Entity<DamageableComponent> ccc)
    {
        return (int) _damageable.GetTotalDamage((ccc.Owner, ccc.Comp)).Float();
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
        var canStart = _waveRule.IsPrep()
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

    private void OnPrepStarted(ref WavePrepStartedEvent ev) { PushCCCState(); BroadcastCanStartWave(); _lastBroadcastTime.Clear(); }
    private void OnCombatStarted(ref WaveCombatStartedEvent ev) { PushCCCState(); BroadcastCanStartWave(); }
    private void OnReadyCheckUpdated(ref ReadyCheckUpdatedEvent ev) { PushCCCState(); BroadcastCanStartWave(); }

    private void OnStartWave(EntityUid uid, FinalStandCCCComponent comp, CCCStartWaveMessage args)
    {
        if (!_waveRule.IsPrep()) return;
        var jobId = GetJobId(args.Actor);
        if (jobId == null || !ReadyCheckDepts.IsCaptain(jobId)) return;
        _waveRule.RequestWaveStart();
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
            ? GetDamage((cccUid, dmgComp))
            : 0;
        _shownDamage[cccUid] = cccDmg;

        var cccMax = TryComp<FinalStandCCCComponent>(cccUid, out var ccc) ? (int) ccc.MaxHealth : 0;

        var (ready, crew) = _readyUp.Count();
        var state = new CCCBoundUserInterfaceState(
            waveNumber: wave.WaveNumber,
            estimatedEnemyCount: wave.TotalEnemies,
            factionDisplay: wave.FactionDisplay,
            isBossWave: wave.IsBossWave,
            activeSpawnerDirections: wave.SpawnerDirections,
            readiedPlayerCount: ready,
            totalPlayerCount: crew,
            nextWaveEnemyTypes: wave.NextWaveEnemyTypes,
            cccCurrentDamage: cccDmg,
            cccMaxHealth: cccMax);

        _ui.SetUiState(cccUid, CCCUiKey.Key, state);
    }
}
