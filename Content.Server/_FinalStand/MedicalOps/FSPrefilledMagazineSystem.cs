using Content.Server.Chemistry.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSPrefilledMagazineSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSPrefilledMagazineComponent, MapInitEvent>(OnMapInit,
            after: [typeof(SolutionContainerSystem)]);
    }

    private void OnMapInit(Entity<FSPrefilledMagazineComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<SolutionAmmoProviderComponent>(ent, out var provider) || provider.Shots > 0)
            return;

        if (!_solutions.TryGetSolution(ent.Owner, provider.SolutionId, out var soln, out var solution)
            || solution.Volume <= 0)
        {
            return;
        }

        _solutions.UpdateChemicals(soln.Value);
    }
}
