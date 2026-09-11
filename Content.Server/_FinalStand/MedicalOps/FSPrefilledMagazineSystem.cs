using Content.Server.Chemistry.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server._FinalStand.MedicalOps;

// GunSystem computes a solution magazine's shot count on MapInit, but SharedSolutionContainerSystem
// also creates the solutions on MapInit and the two are unordered. A magazine that ships prefilled
// loses that race: its shot count is measured against a solution that does not exist yet, and then
// never recomputes, because a prefilled solution never changes. Ordering the vanilla subscription is
// not possible - GunSystem has other unordered MapInit subscriptions and Robust requires them to
// agree - so the recount is nudged from here instead.
public sealed class FSPrefilledMagazineSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Cannot subscribe on SolutionAmmoProviderComponent: GunSystem already owns that pair and
        // Robust rejects duplicates. A marker of our own also lets us order after the solutions exist.
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
