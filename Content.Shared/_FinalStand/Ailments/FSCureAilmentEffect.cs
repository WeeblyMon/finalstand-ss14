using Content.Shared.EntityEffects;

namespace Content.Shared._FinalStand.Ailments;

public sealed partial class FSCureAilment : EntityEffectBase<FSCureAilment>
{
    [DataField(required: true)]
    public FSAilmentSlot Slot;
}

public sealed partial class FSCureAilmentEntityEffectSystem : EntityEffectSystem<FSAilmentStateComponent, FSCureAilment>
{
    [Dependency] private FSAilmentSystem _ailments = default!;

    protected override void Effect(Entity<FSAilmentStateComponent> entity, ref EntityEffectEvent<FSCureAilment> args)
    {
        _ailments.Cure(entity, args.Effect.Slot, args.User, FSCureKind.Chemical);
    }
}
