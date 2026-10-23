using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry;
using Content.Shared.FixedPoint;
using Content.Shared.Labels.EntitySystems;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSFieldKitStationSystem
{
    [Dependency] private LabelSystem _label = default!;

    private void InitializePackaging()
    {
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitBottleMessage>(OnBottle);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitPillMessage>(OnPill);
    }

    private void OnBottle(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitBottleMessage args)
    {
        if (args.Count <= 0 || !Ready(ent, args.Actor, out _, out var jug, out var jugSolution))
            return;

        var coordinates = Transform(ent).Coordinates;
        for (var i = 0; i < args.Count && jugSolution.Volume > 0; i++)
        {
            var bottle = Spawn(ent.Comp.BottlePrototype, coordinates);
            if (!_solutions.TryGetSolution(bottle, SharedChemMaster.BottleSolutionName, out var soln, out var solution))
            {
                Del(bottle);
                return;
            }

            _solutions.TryAddSolution(soln.Value, _solutions.SplitSolution(jug, FixedPoint2.Min(solution.AvailableVolume, jugSolution.Volume)));
            Finish(ent, bottle, args.Actor);
        }

        Click(ent);
        UpdateUi(ent);
    }

    private void OnPill(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitPillMessage args)
    {
        if (args.Dose <= 0 || args.Dose > ent.Comp.MaxPillDose || args.Count <= 0
            || !Ready(ent, args.Actor, out _, out var jug, out var jugSolution))
            return;

        var count = Math.Min(Math.Min(args.Count, ent.Comp.MaxPillsPerPress), (int) (jugSolution.Volume.Float() / args.Dose));
        var coordinates = Transform(ent).Coordinates;
        for (var i = 0; i < count; i++)
        {
            var pill = Spawn(ent.Comp.PillPrototype, coordinates);
            _solutions.EnsureSolution(pill, SharedChemMaster.PillSolutionName, out var soln);
            soln.Comp.Solution.MaxVolume = args.Dose;
            _solutions.TryAddSolution(soln, _solutions.SplitSolution(jug, args.Dose));
            Finish(ent, pill, args.Actor);
        }

        Click(ent);
        UpdateUi(ent);
    }

    private void Finish(Entity<FSFieldKitStationComponent> ent, EntityUid item, EntityUid user)
    {
        if (ent.Comp.Label.Length > 0)
            _label.Label(item, ent.Comp.Label);
        _attribution.TagProducer(item, user);
    }
}
