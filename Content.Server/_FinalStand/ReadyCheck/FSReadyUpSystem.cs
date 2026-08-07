using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.ReadyCheck;

public sealed class FSReadyUpSystem : EntitySystem
{
    [Dependency] private readonly ReadyCheckSystem _readyCheck = default!;
    [Dependency] private readonly WaveGameRuleSystem _waveRule = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    // Players who have already shaved time this prep phase; cleared each new phase.
    private readonly HashSet<EntityUid> _hasContributed = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
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

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        // Late-join: show the ready-up widget if still in prep and refresh the active count.
        if (!_readyCheck.IsCombatPhase())
        {
            _readyCheck.SetTotalPlayers(CountActivePlayers());
            RaiseNetworkEvent(new WavePhaseChangedEvent(true), Filter.SinglePlayer(ev.Player));
            var isReady = ev.Mob != default && _readyCheck.IsPlayerReady(ev.Mob);
            RaiseNetworkEvent(
                new FSReadyUpStateEvent(_readyCheck.GetReadyCount(), _readyCheck.GetTotalCount(), isReady),
                ev.Player);
        }
    }

    private void OnReadyUpRequest(FSReadyUpRequestMessage msg, EntitySessionEventArgs args)
    {
        if (_readyCheck.IsCombatPhase()) return;
        if (args.SenderSession.AttachedEntity is not { } player) return;

        _readyCheck.SetTotalPlayers(CountActivePlayers());
        _readyCheck.SetPlayerReady(player, msg.IsReady);

        // Each player may only shave time once per prep phase, even if they toggle ready/unready.
        if (msg.IsReady && _hasContributed.Add(player))
        {
            var prepComp = _waveRule.GetPrepComponent();
            var total = _readyCheck.GetTotalCount();
            if (prepComp != null && total > 0)
                _waveRule.ReducePrepTimeBy(prepComp.PrepDuration.TotalSeconds / total);
        }

        BroadcastState();

        if (_readyCheck.HasMajority())
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
