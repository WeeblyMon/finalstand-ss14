using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.ReadyCheck;

public sealed partial class FSReadyUpSystem : EntitySystem
{
    [Dependency] private ReadyCheckSystem _readyCheck = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    // Players who have already shaved time this prep phase; cleared each new phase.
    private readonly HashSet<EntityUid> _hasContributed = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeNetworkEvent<FSWaveHudRequestEvent>(OnHudRequest);
        SubscribeNetworkEvent<FSReadyUpRequestMessage>(OnReadyUpRequest);
    }

    private void OnPrepStarted(WavePrepStartedEvent ev)
    {
        _hasContributed.Clear();
        _readyCheck.ResetReadyStates();
        _readyCheck.SetTotalPlayers(CountActivePlayers());
        RaiseNetworkEvent(new WavePhaseChangedEvent(true), Filter.Broadcast());
        BroadcastState();
    }

    private void OnCombatStarted(WaveCombatStartedEvent ev)
    {
        RaiseNetworkEvent(new WavePhaseChangedEvent(false), Filter.Broadcast());
        BroadcastState();
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev) => SendPrepState(ev.Player, ev.Mob);

    private void OnHudRequest(FSWaveHudRequestEvent ev, EntitySessionEventArgs args)
        => SendPrepState(args.SenderSession, args.SenderSession.AttachedEntity ?? default);

    private void SendPrepState(ICommonSession player, EntityUid mob)
    {
        if (!_waveRule.TryGetWaveStatus(out _, out var phase) || phase != WavePhase.Prep || _readyCheck.IsCombatPhase())
            return;

        _readyCheck.SetTotalPlayers(CountActivePlayers());
        RaiseNetworkEvent(new WavePhaseChangedEvent(true), Filter.SinglePlayer(player));
        var isReady = mob != default && _readyCheck.IsPlayerReady(mob);
        RaiseNetworkEvent(
            new FSReadyUpStateEvent(_readyCheck.GetReadyCount(), _readyCheck.GetTotalCount(), isReady),
            player);
    }

    private void OnReadyUpRequest(FSReadyUpRequestMessage msg, EntitySessionEventArgs args)
    {
        if (_readyCheck.IsCombatPhase()) return;
        if (args.SenderSession.AttachedEntity is not { } player) return;

        _readyCheck.SetPlayerReady(player, msg.IsReady);

        _readyCheck.SetTotalPlayers(CountActivePlayers());
        var majority = _readyCheck.HasMajority();

        // Each player may only shave time once per prep phase, even if they toggle ready/unready.
        if (msg.IsReady && !majority && _hasContributed.Add(player))
        {
            var prepComp = _waveRule.GetPrepComponent();
            var total = _readyCheck.GetTotalCount();
            if (prepComp != null && total > 0)
                _waveRule.ReducePrepTimeBy(prepComp.PrepDuration.TotalSeconds / total);
        }

        BroadcastState();

        if (majority)
            RaiseLocalEvent(new WaveStartRequestEvent());
    }

    private void BroadcastState()
    {
        _readyCheck.SetTotalPlayers(CountActivePlayers());
        var readyCount = _readyCheck.GetReadyCount();
        var totalCount = _readyCheck.GetTotalCount();

        foreach (var session in _playerManager.Sessions)
        {
            var isReady = session.AttachedEntity.HasValue &&
                          _readyCheck.IsPlayerReady(session.AttachedEntity.Value);
            RaiseNetworkEvent(new FSReadyUpStateEvent(readyCount, totalCount, isReady), session);
        }
    }

    private int CountActivePlayers()
    {
        var count = 0;
        foreach (var session in _playerManager.Sessions)
            if (session.AttachedEntity.HasValue) count++;
        return count;
    }
}
