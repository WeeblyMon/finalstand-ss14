using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.EntityEffects.Effects.Botany;

public sealed partial class PlantMutateSpeciesChangeEntityEffectSystem : EntityEffectSystem<PlantDataComponent, PlantMutateSpeciesChange>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private PlantMutationSystem _mutation = default!;

    protected override void Effect(Entity<PlantDataComponent> entity, ref EntityEffectEvent<PlantMutateSpeciesChange> args)
    {
        if (entity.Comp.MutationPrototypes.Count == 0)
            return;

        var random = SharedRandomExtensions.PredictedRandom(_timing, GetNetEntity(entity), new NetEntity(9001));
        var newPlantEnt = random.Pick(entity.Comp.MutationPrototypes);
        _mutation.SpeciesChange(entity.Owner, newPlantEnt);
    }
}
public sealed partial class PlantMutateSpeciesChange : EntityEffectBase<PlantMutateSpeciesChange>;
