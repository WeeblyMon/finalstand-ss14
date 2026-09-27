using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed class FSSyringeGunExamineSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSyringeGunComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<FSSyringeGunComponent> ent, ref ExaminedEvent args)
    {
        if (!TryGetLoad(ent, out var mix, out var volume))
        {
            args.PushMarkup(Loc.GetString("fs-syringe-gun-examine-empty"));
            return;
        }

        args.PushMarkup(Loc.GetString("fs-syringe-gun-examine-loaded", ("mix", mix), ("units", volume.Int())));
    }

    public bool TryGetLoad(Entity<FSSyringeGunComponent> gun, out string mix, out FixedPoint2 volume)
    {
        mix = string.Empty;
        volume = FixedPoint2.Zero;

        if (!_containers.TryGetContainer(gun, SharedGunSystem.MagazineSlot, out var container)
            || container is not ContainerSlot { ContainedEntity: { } magazine })
            return false;

        if (!_solutions.TryGetSolution(magazine, gun.Comp.MagazineSolution, out _, out var solution)
            || solution.Volume <= 0)
        {
            mix = Loc.GetString("fs-syringe-filler-empty");
            return true;
        }

        volume = solution.Volume;
        mix = solution.Contents.Count > 1
            ? Loc.GetString("fs-solution-label-mixed")
            : solution.GetPrimaryReagentId() is { } primary && _prototypes.TryIndex<ReagentPrototype>(primary.Prototype, out var proto)
                ? proto.LocalizedName
                : Loc.GetString("fs-syringe-filler-unknown");
        return true;
    }
}
