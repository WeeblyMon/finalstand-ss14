using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Ailments;
using Content.Shared.EntityEffects;

namespace Content.Server._FinalStand.Ailments;

public sealed partial class FSApplyWardEntityEffectSystem : EntityEffectSystem<FSAilmentStateComponent, FSApplyWard>
{
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private FSMedicalUpgradeSystem _upgrades = default!;

    protected override void Effect(Entity<FSAilmentStateComponent> entity, ref EntityEffectEvent<FSApplyWard> args)
    {
        if (args.Effect.RequiresNode is { } node && !_upgrades.Unlocked(node))
            return;

        _ailments.ApplyWard(entity, args.Effect.Ward);
    }
}
