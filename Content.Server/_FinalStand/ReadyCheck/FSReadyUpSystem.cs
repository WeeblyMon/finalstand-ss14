using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.ReadyCheck;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.GameTicking;
using Content.Shared.Ghost.Components;
using Content.Shared.Mobs.Components;
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
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
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
            if (!IsCrew(session))
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
        BroadcastState();
    }

    private void OnCombatStarted(ref WaveCombatStartedEvent ev)
    {
        BroadcastState();
    }

    // Spawning, respawning, reconnecting, ghosting and leaving all change the crew. A reconnected client also
    // starts with a blank HUD and needs its own ready state again.
    private void OnPlayerAttached(PlayerAttachedEvent ev) => OnCrewChanged();

    private void OnPlayerDetached(PlayerDetachedEvent ev) => OnCrewChanged();

    private void OnCrewChanged()
    {
        if (!_waveRule.IsPrep())
            return;

        BroadcastState();

        // Someone not ready leaving can tip the rest into a majority.
        var (ready, crew) = Count();
        if (ready > crew / 2)
            _waveRule.RequestWaveStart();
    }

    private void OnReadyUpRequest(FSReadyUpRequestMessage msg, EntitySessionEventArgs args)
    {
        var session = args.SenderSession;
        if (!_waveRule.IsPrep() || !IsCrew(session))
            return;

        // A repeat press changes nothing, and each change goes out to every session.
        var changed = msg.IsReady ? _ready.Add(session.UserId) : _ready.Remove(session.UserId);
        if (!changed)
            return;

        var (ready, crew) = Count();
        if (msg.IsReady && _shaved.Add(session.UserId))
            _waveRule.ShavePrepTime(crew);

        BroadcastState();

        if (ready > crew / 2)
            _waveRule.RequestWaveStart();
    }

    // Someone in a body. Ghosts and observers can't ready up or hold the majority back.
    private bool IsCrew(ICommonSession session)
    {
        return session.AttachedEntity is { } body
               && HasComp<MobStateComponent>(body)
               && !HasComp<GhostComponent>(body);
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
