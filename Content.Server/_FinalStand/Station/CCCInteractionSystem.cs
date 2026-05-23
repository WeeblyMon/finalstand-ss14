using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.ReadyCheck;
using Content.Server.Chat.Managers;
using Content.Shared._FinalStand.CCC;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.ReadyCheck;
using Robust.Server.GameObjects;

namespace Content.Server._FinalStand.Station;

public sealed class CCCInteractionSystem : EntitySystem
{
    [Dependency] private readonly WaveGameRuleSystem _waveRule = default!;
    [Dependency] private readonly ReadyCheckSystem _readyCheck = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;

    private float _stateTimer;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<ReadyCheckUpdatedEvent>(OnReadyCheckUpdated);

        Subs.BuiEvents<FinalStandCCCComponent>(CCCUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnCCCOpened);
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
        PushCCCStateTo(uid);
    }

    private void OnPrepStarted(WavePrepStartedEvent ev) => PushCCCState();
    private void OnCombatStarted(WaveCombatStartedEvent ev) => PushCCCState();
    private void OnReadyCheckUpdated(ReadyCheckUpdatedEvent ev) => PushCCCState();

    private void OnStartWave(EntityUid uid, FinalStandCCCComponent comp, CCCStartWaveMessage args)
    {
        if (_readyCheck.IsCombatPhase()) return;
        RaiseLocalEvent(new WaveStartRequestEvent());
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
