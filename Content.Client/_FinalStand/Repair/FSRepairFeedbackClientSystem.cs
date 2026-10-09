// Shows a repairer's summed "+N" over the structure, and ticks and pulses the cap marker when a rivet hits the cap.
using Content.Client._FinalStand.DamageNumbers;
using Content.Shared._FinalStand.Repair;
using Content.Shared.CCVar;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Repair;

public sealed class FSRepairFeedbackClientSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private FSDamageNumberSystem _numbers = default!;

    private static readonly SoundSpecifier CapSound = new SoundPathSpecifier("/Audio/_FinalStand/Engineering/cap_tick.ogg",
        AudioParams.Default.WithPitchScale(0.8f));

    public EntityUid? CapTarget { get; private set; }
    public TimeSpan CapAt { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSRepairNumberEvent>(OnNumber);
        SubscribeNetworkEvent<FSRivetCapEvent>(OnCap);
    }

    private void OnNumber(FSRepairNumberEvent ev)
    {
        if (_cfg.GetCVar(CCVars.FSEngRepairFeedback) && ev.Amount >= 0.5f)
            _numbers.SpawnHeal(ev.Target, ev.Amount);
    }

    private void OnCap(FSRivetCapEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.FSEngRepairFeedback))
            return;

        CapTarget = GetEntity(ev.Target);
        CapAt = _timing.RealTime;
        _audio.PlayGlobal(CapSound, Filter.Local(), false);
    }
}
