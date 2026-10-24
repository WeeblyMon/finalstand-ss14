using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.MedicalOps;

// Wave enemies dying near a carried chem launcher refill its reserve, more for tougher enemies.
public sealed partial class FSHarvestSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private const float CreditsPerReserve = 30f;
    private const int FallbackKillCredits = 100;

    private static readonly SoundSpecifier TickSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/harvest_tick.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSWaveEnemyDiedEvent>(OnWaveEnemyDied);
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
    }

    private void OnPrepStarted(WavePrepStartedEvent args)
    {
        var query = EntityQueryEnumerator<FSChemLauncherComponent>();
        while (query.MoveNext(out var uid, out var launcher))
        {
            if (launcher.Reserve >= launcher.MaxReserve)
                continue;

            launcher.Reserve = launcher.MaxReserve;
            Dirty(uid, launcher);
        }
    }

    private void OnWaveEnemyDied(ref FSWaveEnemyDiedEvent args)
    {
        if (TerminatingOrDeleted(args.Enemy))
            return;

        var credits = TryComp<FSEnemyValueComponent>(args.Enemy, out var value) ? value.KillCredits : FallbackKillCredits;
        var corpse = _xform.GetMapCoordinates(args.Enemy);

        var query = EntityQueryEnumerator<FSChemLauncherComponent>();
        while (query.MoveNext(out var uid, out var launcher))
        {
            if (!IsCarried(uid) || !InRange(uid, corpse, launcher.HarvestRange))
                continue;

            Refill((uid, launcher), credits / CreditsPerReserve * launcher.HarvestScale);
        }
    }

    private bool IsCarried(EntityUid item)
    {
        var parent = Transform(item).ParentUid;

        for (var depth = 0; depth < 5 && parent.IsValid(); depth++)
        {
            if (HasComp<FSFriendlyFireComponent>(parent))
                return true;

            parent = Transform(parent).ParentUid;
        }

        return false;
    }

    private bool InRange(EntityUid item, MapCoordinates corpse, float range)
    {
        var carrier = _xform.GetMapCoordinates(item);

        return carrier.MapId == corpse.MapId
               && (carrier.Position - corpse.Position).LengthSquared() <= range * range;
    }

    private void Refill(Entity<FSChemLauncherComponent> launcher, float amount)
    {
        if (amount <= 0f || launcher.Comp.Reserve >= launcher.Comp.MaxReserve)
            return;

        launcher.Comp.Reserve = MathF.Min(launcher.Comp.MaxReserve, launcher.Comp.Reserve + amount);
        Dirty(launcher);

        _audio.PlayPvs(TickSound, launcher.Owner, AudioParams.Default.WithVolume(-10f));
    }
}
