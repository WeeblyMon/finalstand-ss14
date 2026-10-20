using Content.Server._FinalStand.Mobs;
using Content.Server.Chat.Managers;
using Content.Server.GameTicking.Rules;
using Content.Server.Light.EntitySystems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.Doors.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Light.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.GameTicking.Rules;

// The wave rule decides when a wave starts and ends; this decides whether it's dark and runs the blackout.
public sealed partial class FSDarkWaveSystem : GameRuleSystem<FSDarkWaveRuleComponent>
{
    [Dependency] private IChatManager _chatManager = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private PoweredLightSystem _poweredLight = default!;
    [Dependency] private SharedPowerReceiverSystem _powerReceiver = default!;
    [Dependency] private ApcSystem _apc = default!;

    private static readonly TimeSpan BlackoutCheckInterval = TimeSpan.FromSeconds(1);
    private static readonly Color WarningColor = Color.FromHex("#8800FF");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadioSendAttemptEvent>(OnRadioSendAttempt);
        SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        SubscribeLocalEvent<FSRevenantExecutedEvent>(OnRevenantExecuted);
    }

    protected override void Ended(EntityUid uid, FSDarkWaveRuleComponent comp, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        End((uid, comp));
    }

    public bool IsDarkWaveActive()
    {
        var query = QueryActiveRules();
        while (query.MoveNext(out _, out _, out var comp, out _))
        {
            if (comp.Active)
                return true;
        }

        return false;
    }

    public bool IsActive(EntityUid rule)
    {
        return TryComp<FSDarkWaveRuleComponent>(rule, out var comp) && comp.Active;
    }

    // Called at the start of every prep to decide whether the coming wave is dark.
    public void Roll(EntityUid rule, int wave, bool bossWave)
    {
        if (!TryComp<FSDarkWaveRuleComponent>(rule, out var comp))
            return;

        comp.Upcoming = false;
        comp.WarningAt = null;
        // A flicker still in its off window (a revenant execution right before prep) would leave those lights dark.
        RestoreFlickeredLights(comp);

        if (comp.Forced)
        {
            comp.Forced = false;
            comp.Upcoming = true;
        }
        else if (wave == comp.GuaranteedWave)
        {
            comp.Upcoming = true;
        }
        else if (wave >= comp.FirstRandomWave && !bossWave)
        {
            comp.WavesSinceLast++;
            comp.Upcoming = comp.WavesSinceLast >= comp.CooldownWaves && RobustRandom.Prob(comp.Chance);
        }

        if (!comp.Upcoming)
            return;

        comp.WarningAt = Timing.CurTime + comp.WarningDelay;
        Log.Info($"Dark wave rolled for wave {wave}.");
    }

    // In prep the dark wave arms straight away; mid-combat it arms for the next prep.
    public bool TryForce(out bool armedNow)
    {
        armedNow = false;
        var query = QueryActiveRules();
        if (!query.MoveNext(out var uid, out _, out var comp, out _))
            return false;

        if (comp.Active || !TryComp<WaveGameRuleComponent>(uid, out var wave) || wave.Phase != WavePhase.Prep)
        {
            comp.Forced = true;
            return true;
        }

        // Forced too, so a re-roll in the same prep (forcenextwave to another wave) keeps it.
        comp.Forced = true;
        comp.Upcoming = true;
        comp.WarningAt = Timing.CurTime;
        armedNow = true;
        return true;
    }

    // Returns false if the coming wave isn't dark. Otherwise the station goes dark for the returned duration.
    public bool TryBegin(EntityUid rule, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;
        if (!TryComp<FSDarkWaveRuleComponent>(rule, out var comp) || !comp.Upcoming)
            return false;

        comp.Upcoming = false;
        comp.Forced = false;
        comp.Active = true;
        comp.WavesSinceLast = 0;
        comp.WarningAt = null;
        RestoreFlickeredLights(comp);

        CutPower(comp);
        StartAmbience(comp);
        if (comp.StartSound != null)
            _audio.PlayGlobal(comp.StartSound, Filter.Broadcast(), true);

        duration = comp.Duration;
        return true;
    }

    public void End(Entity<FSDarkWaveRuleComponent?> rule)
    {
        if (!Resolve(rule, ref rule.Comp, false) || !rule.Comp.Active)
            return;

        rule.Comp.Active = false;
        RestorePower(rule.Comp);
        StopAmbience(rule.Comp);
    }

    protected override void ActiveTick(EntityUid uid, FSDarkWaveRuleComponent comp, GameRuleComponent gameRule, float frameTime)
    {
        var now = Timing.CurTime;

        if (comp.FlickerRestoreAt is { } restoreAt && now >= restoreAt)
            RestoreFlickeredLights(comp);

        if (comp.Upcoming && comp.WarningAt is { } warningAt && now >= warningAt)
        {
            comp.WarningAt = null;
            comp.NextFlicker = now;
            _chatManager.DispatchServerAnnouncement(Loc.GetString("fs-dark-wave-warning"), WarningColor);
            if (comp.WarningSound != null)
                _audio.PlayGlobal(comp.WarningSound, Filter.Broadcast(), true);
        }

        // Once warned, the lights stutter until the wave starts.
        if (comp.Upcoming && comp.WarningAt == null && comp.FlickerRestoreAt == null && now >= comp.NextFlicker)
        {
            var spread = comp.FlickerIntervalMax - comp.FlickerIntervalMin;
            comp.NextFlicker = now + comp.FlickerIntervalMin + spread * RobustRandom.NextDouble();
            Flicker(comp);
        }

        // Engineers will try to flip the breakers back on.
        if (comp.Active && now >= comp.NextBlackoutCheck)
        {
            comp.NextBlackoutCheck = now + BlackoutCheckInterval;
            foreach (var apc in comp.CutApcs)
            {
                if (TryComp<ApcComponent>(apc, out var apcComp) && apcComp.MainBreakerEnabled
                    && TryComp<PowerNetworkBatteryComponent>(apc, out var battery))
                    _apc.ApcToggleBreaker(apc, apcComp, battery);
            }
        }
    }

    public void FlickerAllLightsOnce()
    {
        var query = QueryActiveRules();
        if (query.MoveNext(out _, out _, out var comp, out _) && comp.FlickerRestoreAt == null)
            Flicker(comp);
    }

    private void OnRevenantExecuted(ref FSRevenantExecutedEvent args)
    {
        FlickerAllLightsOnce();
    }

    private void OnRadioSendAttempt(ref RadioSendAttemptEvent args)
    {
        if (IsDarkWaveActive())
            args.Cancelled = true;
    }

    private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
    {
        if (IsDarkWaveActive())
            args.Cancelled = true;
    }

    private void CutPower(FSDarkWaveRuleComponent comp)
    {
        comp.CutApcs.Clear();
        var apcs = EntityQueryEnumerator<ApcComponent, PowerNetworkBatteryComponent>();
        while (apcs.MoveNext(out var uid, out var apc, out var battery))
        {
            if (!apc.MainBreakerEnabled)
                continue;

            _apc.ApcToggleBreaker(uid, apc, battery);
            comp.CutApcs.Add(uid);
        }

        comp.DepoweredDoors.Clear();
        var doors = EntityQueryEnumerator<DoorComponent, ApcPowerReceiverComponent>();
        while (doors.MoveNext(out var uid, out _, out var receiver))
        {
            if (receiver.PowerDisabled)
                continue;

            _powerReceiver.SetPowerDisabled(uid, true, receiver);
            comp.DepoweredDoors.Add(uid);
        }
    }

    private void RestorePower(FSDarkWaveRuleComponent comp)
    {
        foreach (var uid in comp.CutApcs)
        {
            if (TryComp<ApcComponent>(uid, out var apc) && !apc.MainBreakerEnabled)
                _apc.ApcToggleBreaker(uid, apc);
        }

        foreach (var uid in comp.DepoweredDoors)
        {
            if (TryComp<ApcPowerReceiverComponent>(uid, out var receiver))
                _powerReceiver.SetPowerDisabled(uid, false, receiver);
        }

        comp.CutApcs.Clear();
        comp.DepoweredDoors.Clear();
    }

    private void StartAmbience(FSDarkWaveRuleComponent comp)
    {
        StopAmbience(comp);
        if (comp.AmbienceSound == null)
            return;

        var stream = _audio.PlayGlobal(comp.AmbienceSound, Filter.Broadcast(), true, AudioParams.Default.WithLoop(true));
        comp.AmbienceStream = stream?.Entity;
    }

    private void StopAmbience(FSDarkWaveRuleComponent comp)
    {
        if (comp.AmbienceStream is { } stream)
            _audio.Stop(stream);

        comp.AmbienceStream = null;
    }

    private void Flicker(FSDarkWaveRuleComponent comp)
    {
        comp.FlickeredLights.Clear();
        var query = EntityQueryEnumerator<PoweredLightComponent>();
        while (query.MoveNext(out var uid, out var light))
        {
            if (!light.On || !RobustRandom.Prob(comp.FlickerFraction))
                continue;

            _poweredLight.SetState(uid, false, light);
            comp.FlickeredLights.Add(uid);
        }

        comp.FlickerRestoreAt = Timing.CurTime + comp.FlickerOffTime;
    }

    private void RestoreFlickeredLights(FSDarkWaveRuleComponent comp)
    {
        foreach (var uid in comp.FlickeredLights)
        {
            if (TryComp<PoweredLightComponent>(uid, out var light))
                _poweredLight.SetState(uid, true, light);
        }

        comp.FlickeredLights.Clear();
        comp.FlickerRestoreAt = null;
    }
}
