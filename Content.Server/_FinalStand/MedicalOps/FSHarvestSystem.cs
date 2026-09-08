using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSHarvestSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedTransformSystem _xform = default!;
    [Dependency] private WaveGameRuleSystem _wave = default!;

    private readonly Dictionary<EntityUid, float> _harvestedThisWave = new();
    private int _trackedWave = -1;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _harvestedThisWave.Clear();
        _trackedWave = -1;
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;

        if (!HasComp<WaveSpawnedTagComponent>(args.Target) || TerminatingOrDeleted(args.Target))
            return;

        var wave = _wave.GetWaveNumber();
        if (wave != _trackedWave)
        {
            _trackedWave = wave;
            _harvestedThisWave.Clear();
        }

        var corpse = _xform.GetMapCoordinates(args.Target);

        var query = EntityQueryEnumerator<FSHarvestSatchelComponent>();
        while (query.MoveNext(out var satchel, out var comp))
        {
            if (!InRange(satchel, corpse, comp.Range))
                continue;

            Accrue((satchel, comp));
        }
    }

    private bool InRange(EntityUid satchel, MapCoordinates corpse, float range)
    {
        var carrier = _xform.GetMapCoordinates(satchel);

        return carrier.MapId == corpse.MapId
               && (carrier.Position - corpse.Position).LengthSquared() <= range * range;
    }

    private void Accrue(Entity<FSHarvestSatchelComponent> satchel)
    {
        _harvestedThisWave.TryGetValue(satchel.Owner, out var already);

        var room = satchel.Comp.PerWaveCap - already;
        if (room <= 0f)
            return;

        var amount = MathF.Min(satchel.Comp.PerKill, room);

        if (!_solutions.TryGetSolution(satchel.Owner, satchel.Comp.Solution, out var soln, out _))
            return;

        var added = _solutions.TryAddReagent(soln.Value, satchel.Comp.Reagent, amount, out var accepted);
        if (!added || accepted <= 0)
            return;

        _harvestedThisWave[satchel.Owner] = already + accepted.Float();
    }
}
