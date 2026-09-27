using System.Linq;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Utility;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Labels.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSFieldKitStationSystem
{
    [Dependency] private LabelSystem _label = default!;

    private void InitializePackaging()
    {
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitLoadMagazineMessage>(OnLoadMagazine);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitFillFlasksMessage>(OnFillFlasks);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitBottleMessage>(OnBottle);
        SubscribeLocalEvent<FSFieldKitStationComponent, FSFieldKitPillMessage>(OnPill);
    }

    private IEnumerable<(EntityUid Flask, Entity<SolutionComponent> Solution, FixedPoint2 Capacity)> EmptyFlasks(Entity<FSFieldKitStationComponent> ent)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.RackContainer, out var rack))
            yield break;

        foreach (var flask in rack.ContainedEntities)
        {
            if (TryComp<FSSplashFlaskComponent>(flask, out var comp)
                && _solutions.TryGetSolution(flask, comp.Solution, out var soln, out var solution)
                && solution.Volume <= 0)
                yield return (flask, soln.Value, solution.MaxVolume);
        }
    }

    // Loads straight from the jug, with the filler's rule that a magazine only ever holds one mix.
    private void OnLoadMagazine(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitLoadMagazineMessage args)
    {
        if (!Ready(ent, args.Actor, out _, out var jug, out var jugSolution))
            return;

        if (!FSItemSlots.TryGetSlot(EntityManager, _slots, ent, ent.Comp.MagazineSlot, out var slot)
            || slot.Item is not { } magazine
            || !_solutions.TryGetSolution(magazine, ent.Comp.MagazineSolution, out var pack, out var packSolution))
        {
            Popup(ent, args.Actor, "fs-field-kit-no-magazine");
            return;
        }

        if (packSolution.Volume > 0 && !FSSyringeFillerSystem.SameContents(packSolution, jugSolution))
        {
            Popup(ent, args.Actor, "fs-syringe-filler-mixed");
            return;
        }

        var amount = FixedPoint2.Min(packSolution.AvailableVolume, jugSolution.Volume);
        if (amount <= 0)
            return;

        _solutions.TryAddSolution(pack.Value, _solutions.SplitSolution(jug, amount));
        _solutions.UpdateChemicals(pack.Value);
        _attribution.TagProducer(magazine, args.Actor);
        Click(ent);
        UpdateUi(ent);
    }

    private void OnFillFlasks(Entity<FSFieldKitStationComponent> ent, ref FSFieldKitFillFlasksMessage args)
    {
        if (args.Count <= 0 || !Ready(ent, args.Actor, out _, out var jug, out var jugSolution))
            return;

        var capacity = args.Capacity;
        var flasks = EmptyFlasks(ent).Where(f => f.Capacity == capacity).Take(args.Count).ToList();
        if (flasks.Count == 0 || !_container.TryGetContainer(ent, ent.Comp.RackContainer, out var rack))
            return;

        foreach (var (flask, solution, _) in flasks)
        {
            if (jugSolution.Volume < capacity)
                break;

            _container.Remove(flask, rack);
            _solutions.TryAddSolution(solution, _solutions.SplitSolution(jug, capacity));
            _attribution.TagProducer(flask, args.Actor);
        }

        Click(ent);
        UpdateUi(ent);
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
