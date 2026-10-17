using Content.Server.Chat.Systems;
using Content.Server.Destructible;
using Content.Server._FinalStand.Effects;
using Content.Server.RoundEnd;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.CCC;
using Content.Shared.Damage.Systems;
using Content.Shared.Destructible;
using Content.Shared.FixedPoint;
using Content.Shared._FinalStand.Particles;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.CCC;

public sealed partial class FinalStandCCCSystem : EntitySystem
{
    [Dependency] private RoundEndSystem _roundEnd = default!;
    [Dependency] private DestructibleSystem _destructible = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private FSFlairSystem _flair = default!;
    [Dependency] private WaveGameRuleSystem _waves = default!;

    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan RoundEndCountdown = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan ShieldTime = TimeSpan.FromSeconds(5);
    private static readonly Color AlarmColor = Color.FromHex("#e4512f");
    private static readonly SoundSpecifier LifeLostSound = new SoundPathSpecifier("/Audio/Misc/redalert.ogg");
    private static readonly SoundSpecifier LastLifeSound = new SoundPathSpecifier("/Audio/Announcements/bloblarm.ogg");
    private static readonly ProtoId<FSParticleEmitterPrototype> LifeLostRing = "FSCCCLifeLostRing";
    private static readonly ProtoId<FSParticleEmitterPrototype> LifeLostSparks = "FSCCCLifeLostSparks";

    private TimeSpan _nextBroadcast;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FinalStandCCCComponent, MapInitEvent>(OnCCCMapInit);
        SubscribeLocalEvent<FinalStandCCCComponent, DestructionEventArgs>(OnCCCDestroyed);
        SubscribeLocalEvent<FinalStandCCCComponent, DamageChangedEvent>(OnCCCDamaged, before: [typeof(DestructibleSystem)]);
        SubscribeLocalEvent<FinalStandCCCComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        var query = EntityQueryEnumerator<FinalStandCCCComponent, FinalStandCCCTagComponent>();
        while (query.MoveNext(out var uid, out var comp, out var tag))
        {
            if (comp.HealFractionPerWave <= 0f || tag.MaxHealth <= 0f)
                continue;

            _damageable.HealEvenly(uid, FixedPoint2.New(-tag.MaxHealth * comp.HealFractionPerWave));
        }
    }

    private void OnCCCMapInit(EntityUid uid, FinalStandCCCComponent comp, MapInitEvent args)
    {
        if (!TryComp<FinalStandCCCTagComponent>(uid, out var tag))
            return;

        tag.MaxHealth = _destructible.TryGetDestroyedAt(uid, out var destroyedAt)
            ? destroyedAt.Value.Float()
            : 0f;
        tag.LivesLeft = tag.Lives;

        Dirty(uid, tag);
    }

    private void OnCCCDestroyed(EntityUid uid, FinalStandCCCComponent comp, DestructionEventArgs args)
    {
        Log.Info("Central Command Console destroyed. Ending round.");
        _roundEnd.EndRound(RoundEndCountdown);
    }

    private void OnBeforeDamage(Entity<FinalStandCCCComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (TryComp<FinalStandCCCTagComponent>(ent, out var tag) && _timing.CurTime < tag.ShieldUntil && args.Damage.AnyPositive())
            args.Cancelled = true;
    }

    private void OnCCCDamaged(EntityUid uid, FinalStandCCCComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        if (TryComp<FinalStandCCCTagComponent>(uid, out var tag)
            && tag.MaxHealth > 0f
            && tag.LivesLeft > 1
            && _damageable.GetTotalDamage((uid, args.Damageable)).Float() >= tag.MaxHealth)
        {
            LoseLife(uid, tag);
            return;
        }

        var now = _timing.CurTime;
        if (now < _nextBroadcast)
            return;

        _nextBroadcast = now + BroadcastInterval;
        RaiseNetworkEvent(new CCCUnderAttackEvent(), Filter.Broadcast());
    }

    private void LoseLife(EntityUid uid, FinalStandCCCTagComponent tag)
    {
        tag.LivesLeft--;
        tag.ShieldUntil = _timing.CurTime + ShieldTime;
        Dirty(uid, tag);
        _damageable.ClearAllDamage(uid);

        _flair.Burst(LifeLostRing, uid, kick: 2.5f);
        _flair.Burst(LifeLostSparks, uid);
        var wave = _waves.FailWave() ?? 0;

        var last = tag.LivesLeft == 1;
        _chat.DispatchGlobalAnnouncement(
            Loc.GetString(last ? "fs-ccc-last-life" : "fs-ccc-life-lost", ("left", tag.LivesLeft), ("wave", wave)),
            Loc.GetString("fs-ccc-announcer"),
            announcementSound: last ? LastLifeSound : LifeLostSound,
            colorOverride: AlarmColor);
        RaiseNetworkEvent(new FSCCCLifeLostEvent(GetNetEntity(uid), tag.LivesLeft, tag.Lives), Filter.Broadcast());
    }
}
