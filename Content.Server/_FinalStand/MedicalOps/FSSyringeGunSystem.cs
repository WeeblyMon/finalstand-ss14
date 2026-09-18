using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSSyringeGunSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSyringeGunComponent, AmmoShotEvent>(OnAmmoShot);
    }

    private void OnAmmoShot(Entity<FSSyringeGunComponent> ent, ref AmmoShotEvent args)
    {
        if (!_containers.TryGetContainer(ent.Owner, SharedGunSystem.MagazineSlot, out var container)
            || container is not ContainerSlot { ContainedEntity: { } magazine })
        {
            return;
        }

        if (!TryComp<SolutionAmmoProviderComponent>(magazine, out var provider)
            || !_solutions.TryGetSolution(magazine, ent.Comp.MagazineSolution, out var source, out var sourceSolution))
        {
            return;
        }

        foreach (var dart in args.FiredProjectiles)
        {
            if (sourceSolution.Volume <= 0)
                break;

            if (!_solutions.TryGetSolution(dart, ent.Comp.DartSolution, out var target, out _))
                continue;

            var take = FixedPoint2.Min(provider.FireCost, sourceSolution.Volume);
            if (take <= FixedPoint2.Zero)
                break;

            var drawn = _solutions.SplitSolution(source.Value, take);
            _solutions.TryAddSolution(target.Value, drawn);
        }
    }
}
