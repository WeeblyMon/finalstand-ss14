using Content.Shared.Labels.EntitySystems;
using Content.Server.Chemistry.EntitySystems;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

public sealed class FSSolutionLabelSystem : EntitySystem
{
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private LabelSystem _label = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSolutionLabelComponent, SolutionChangedEvent>(OnSolutionChanged);
        SubscribeLocalEvent<FSSolutionLabelComponent, MapInitEvent>(OnMapInit,
            after: [typeof(SolutionContainerSystem)]);
    }

    private void OnMapInit(Entity<FSSolutionLabelComponent> ent, ref MapInitEvent args) => Refresh(ent);

    private void OnSolutionChanged(Entity<FSSolutionLabelComponent> ent, ref SolutionChangedEvent args) => Refresh(ent);

    private void Refresh(Entity<FSSolutionLabelComponent> ent)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution)
            || solution.Volume <= 0)
        {
            _label.Label(ent, null);
            return;
        }

        if (solution.Contents.Count > 1)
        {
            _label.Label(ent, Loc.GetString("fs-solution-label-mixed"));
            return;
        }

        if (solution.GetPrimaryReagentId() is not { } primary
            || !_prototypes.TryIndex<ReagentPrototype>(primary.Prototype, out var proto))
        {
            _label.Label(ent, null);
            return;
        }

        _label.Label(ent, proto.LocalizedName);
    }
}
