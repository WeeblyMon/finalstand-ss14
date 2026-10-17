using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.ReadyCheck;

// Crew ready up during prep; each first ready-up shaves prep time and a majority starts the wave.
public sealed partial class FSReadyUpSystem : EntitySystem
{
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private readonly HashSet<NetUserId> _ready = new();

    // Only the first ready-up per prep shaves time, so toggling can't drain the clock.
    private readonly HashSet<NetUserId> _shaved = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<FSReadyUpRequestMessage>(OnReadyUpRequest);
    }

    // Counted against the live crew, so players who readied up and then left don't count.
    public (int Ready, int Crew) Count()
    {
        var ready = 0;
        var crew = 0;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity == null)
                continue;

            crew++;
            if (_ready.Contains(session.UserId))
                ready++;
        }

        return (ready, crew);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _ready.Clear();
        _shaved.Clear();
    }

    private void OnPrepStarted(ref WavePrepStartedEvent ev)
    {
        _ready.Clear();
        _shaved.Clear();
        RaiseNetworkEvent(new WavePhaseChangedEvent(true), Filter.Broadcast());
        BroadcastState();
    }

    private void OnCombatStarted(ref WaveCombatStartedEvent ev)
    {
        RaiseNetworkEvent(new WavePhaseChangedEvent(false), Filter.Broadcast());
        BroadcastState();
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_waveRule.IsPrep())
            return;

        // Late joiners need the ready-up widget, and everyone else's total just grew.
        RaiseNetworkEvent(new WavePhaseChangedEvent(true), Filter.SinglePlayer(ev.Player));
        BroadcastState();
    }

    private void OnReadyUpRequest(FSReadyUpRequestMessage msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_waveRule.IsPrep() || session.AttachedEntity == null)
            return;

        if (msg.IsReady)
            _ready.Add(session.UserId);
        else
            _ready.Remove(session.UserId);

        var (ready, crew) = Count();
        if (msg.IsReady && _shaved.Add(session.UserId))
            _waveRule.ShavePrepTime(crew);

        BroadcastState();

        if (ready > crew / 2)
            _waveRule.RequestWaveStart();
    }

    private void BroadcastState()
    {
        var (ready, crew) = Count();
        foreach (var session in _playerManager.Sessions)
        {
            RaiseNetworkEvent(new FSReadyUpStateEvent(ready, crew, _ready.Contains(session.UserId)), session);
        }

        var ev = new ReadyCheckUpdatedEvent();
        RaiseLocalEvent(ref ev);
    }
}
