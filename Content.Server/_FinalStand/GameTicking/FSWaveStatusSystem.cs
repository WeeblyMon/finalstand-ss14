using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.GameTicking;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.GameTicking;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.GameTicking;

/// <summary>
/// Publishes the current wave to the lobby and the hub status (preset line).
/// </summary>
public sealed partial class FSWaveStatusSystem : EntitySystem
{
    [Dependency] private GameTicker _ticker = default!;
    [Dependency] private WaveGameRuleSystem _waveRule = default!;

    private int _wave;
    private WavePhase _phase;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Set(0, WavePhase.Prep));
        SubscribeNetworkEvent<FSWaveStatusRequestEvent>(OnRequest);
    }

    private void OnPrepStarted(ref WavePrepStartedEvent ev) => Refresh();

    private void OnCombatStarted(ref WaveCombatStartedEvent ev) => Refresh();

    private void Refresh()
    {
        if (_waveRule.TryGetWaveStatus(out var wave, out var phase))
            Set(wave, phase);
        else
            Set(0, WavePhase.Prep);
    }

    private void Set(int wave, WavePhase phase)
    {
        _wave = wave;
        _phase = phase;
        RaiseNetworkEvent(new FSWaveStatusEvent(wave, phase), Filter.Broadcast());
        _ticker.SetStatusPresetSuffix(wave > 0
            ? Loc.GetString("fs-hub-wave-suffix", ("wave", wave))
            : null);
    }

    private void OnRequest(FSWaveStatusRequestEvent ev, EntitySessionEventArgs args)
    {
        RaiseNetworkEvent(new FSWaveStatusEvent(_wave, _phase), Filter.SinglePlayer(args.SenderSession));
    }
}
