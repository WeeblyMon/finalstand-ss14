// Stamps DNA into blood. Server-only because forensics is not predicted.

using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Forensics;

namespace Content.Server.Body.Systems;

public sealed class BloodstreamDnaSystem : EntitySystem
{
    [Dependency] private readonly Content.Shared.Body.Systems.BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodstreamComponent, GenerateDnaEvent>(OnDnaGenerated);
    }

    private void OnDnaGenerated(Entity<BloodstreamComponent> entity, ref GenerateDnaEvent args)
    {
        if (!_solutionContainer.ResolveSolution(entity.Owner,
                entity.Comp.BloodSolutionName,
                ref entity.Comp.BloodSolution,
                out var bloodSolution))
        {
            Log.Error("Unable to set bloodstream DNA, solution entity could not be resolved");
            return;
        }

        var data = _bloodstream.GetEntityBloodData(entity.Owner);

        foreach (var reagent in bloodSolution.Contents)
        {
            var reagentData = reagent.Reagent.EnsureReagentData();
            reagentData.RemoveAll(x => x is DnaData);
            reagentData.AddRange(data);
        }
    }
}
