using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

// OverHealer shields: filled by healing the soft cap withholds, spent before health, fading once unfed.
public sealed partial class FSOverhealSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly TimeSpan DecayGrace = TimeSpan.FromSeconds(2);

    private static readonly SoundSpecifier BreakSound = new SoundPathSpecifier("/Audio/Effects/glass_break1.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSIncomingDamageModifyEvent>(OnIncomingDamage, after: [typeof(FSIncomingDamagePerkSystem)]);
        SubscribeLocalEvent<FSOverhealComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    public void Feed(EntityUid patient, FSMediGunComponent gun, float amount)
    {
        if (gun.OverhealRatio <= 0f
            || !_thresholds.TryGetThresholdForState(patient, MobState.Critical, out var threshold)
            || threshold is not { } crit)
            return;

        var shield = EnsureComp<FSOverhealComponent>(patient);
        shield.Max = MathF.Max(shield.Max, crit.Float() * gun.OverhealRatio);
        shield.Amount = MathF.Min(shield.Max, shield.Amount + amount);
        shield.DecayPerSecond = gun.OverhealDecay;
        shield.SourceColor = gun.BeamColor;
        shield.LastFed = _timing.CurTime;
        Dirty(patient, shield);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSOverhealComponent>();
        while (query.MoveNext(out var uid, out var shield))
        {
            if (now - shield.LastFed < DecayGrace)
                continue;

            var before = (int) shield.Amount;
            shield.Amount -= shield.Max * shield.DecayPerSecond * frameTime;

            if (shield.Amount <= 0f)
            {
                RemCompDeferred<FSOverhealComponent>(uid);
                continue;
            }

            if ((int) shield.Amount != before)
                Dirty(uid, shield);
        }
    }

    private void OnIncomingDamage(ref FSIncomingDamageModifyEvent ev)
    {
        if (!TryComp<FSOverhealComponent>(ev.Target, out var shield) || shield.Amount <= 0f)
            return;

        var total = ev.Args.Damage.GetTotal().Float();
        if (total <= 0f)
            return;

        var absorbed = MathF.Min(shield.Amount, total);
        ev.Args.Damage *= (total - absorbed) / total;
        shield.Amount -= absorbed;

        if (shield.Amount > 0f)
        {
            Dirty(ev.Target, shield);
            return;
        }

        _audio.PlayPvs(BreakSound, ev.Target, AudioParams.Default.WithVolume(-4f));
        RaiseNetworkEvent(new FSOverhealBrokenEvent(GetNetEntity(ev.Target)));
        RemCompDeferred<FSOverhealComponent>(ev.Target);
    }

    private void OnMobStateChanged(Entity<FSOverhealComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            RemCompDeferred<FSOverhealComponent>(ent);
    }
}
