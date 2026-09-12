using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Shared.Map;

using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSHarvestSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private WaveGameRuleSystem _wave = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier TickSound =
        new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/harvest_tick.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        var query = EntityQueryEnumerator<FSHarvestSatchelComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.AccruedThisWave = 0f;
            comp.TrackedWave = -1;
            Dirty(uid, comp);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        if (!HasComp<WaveSpawnedTagComponent>(args.Target) || TerminatingOrDeleted(args.Target))
            return;

        var wave = _wave.GetWaveNumber();
        var corpse = _xform.GetMapCoordinates(args.Target);

        var query = EntityQueryEnumerator<FSHarvestSatchelComponent>();
        while (query.MoveNext(out var satchel, out var comp))
        {
            if (comp.TrackedWave != wave)
            {
                comp.TrackedWave = wave;
                comp.AccruedThisWave = 0f;
                Dirty(satchel, comp);
            }

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
