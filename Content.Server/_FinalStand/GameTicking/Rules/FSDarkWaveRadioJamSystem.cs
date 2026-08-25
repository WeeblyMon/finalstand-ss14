using Content.Server.Radio;

namespace Content.Server._FinalStand.GameTicking.Rules;

public sealed class FSDarkWaveRadioJamSystem : EntitySystem
{
    [Dependency] private WaveGameRuleSystem _waveRule = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnSendAttempt);
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnReceiveAttempt);
    }

    private void OnSendAttempt(ref RadioSendAttemptEvent args)
    {
        if (_waveRule.IsDarkWaveActive())
            args.Cancelled = true;
    }

    private void OnReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (_waveRule.IsDarkWaveActive())
            args.Cancelled = true;
    }
}
