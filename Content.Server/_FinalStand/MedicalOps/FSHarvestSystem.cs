using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.GameTicking;
using Robust.Shared.Map;

using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSHarvestSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier TickSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/harvest_tick.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSWaveEnemyDiedEvent>(OnWaveEnemyDied);
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    // The cap is per wave, replays after a lost CCC life included, which keep the wave number.
    private void OnPrepStarted(ref WavePrepStartedEvent args)
    {
        ResetCaps();
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        ResetCaps();
    }

    private void ResetCaps()
    {
        var query = EntityQueryEnumerator<FSHarvestSatchelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.AccruedThisWave == 0f)
                continue;

            comp.AccruedThisWave = 0f;
            Dirty(uid, comp);
        }
    }

    private void OnWaveEnemyDied(ref FSWaveEnemyDiedEvent args)
    {
        if (TerminatingOrDeleted(args.Enemy))
            return;

        var corpse = _xform.GetMapCoordinates(args.Enemy);

        var query = EntityQueryEnumerator<FSHarvestSatchelComponent>();
        while (query.MoveNext(out var satchel, out var comp))
        {
            if (!IsCarried(satchel) || !InRange(satchel, corpse, comp.Range))
                continue;

            Accrue((satchel, comp));
        }
    }

    private bool IsCarried(EntityUid satchel)
    {
        var parent = Transform(satchel).ParentUid;

        for (var depth = 0; depth < 5 && parent.IsValid(); depth++)
        {
            if (HasComp<FSFriendlyFireComponent>(parent))
                return true;

            parent = Transform(parent).ParentUid;
        }

        return false;
    }

    private bool InRange(EntityUid satchel, MapCoordinates corpse, float range)
    {
        var carrier = _xform.GetMapCoordinates(satchel);

        return carrier.MapId == corpse.MapId
               && (carrier.Position - corpse.Position).LengthSquared() <= range * range;
    }

    private void Accrue(Entity<FSHarvestSatchelComponent> satchel)
    {
        var room = satchel.Comp.PerWaveCap - satchel.Comp.AccruedThisWave;
        if (room <= 0f)
            return;

        var amount = MathF.Min(satchel.Comp.PerKill, room);

        if (!_solutions.TryGetSolution(satchel.Owner, satchel.Comp.Solution, out var soln, out _))
            return;

        var added = _solutions.TryAddReagent(soln.Value, satchel.Comp.Reagent, amount, out var accepted);
        if (!added || accepted <= 0)
            return;

        satchel.Comp.AccruedThisWave += accepted.Float();
        Dirty(satchel);

        _audio.PlayPvs(TickSound, satchel.Owner, AudioParams.Default.WithVolume(-10f));
    }
}
