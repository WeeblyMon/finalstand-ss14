using Content.Shared.FixedPoint;
using Content.Shared.Database;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects;

/// <summary>
/// Base args for the old Effect() API used by _Shitmed effects.
/// </summary>
public class EntityEffectBaseArgs
{
    public readonly EntityUid TargetEntity;
    public readonly IEntityManager EntityManager;
    public readonly EntityUid? User;

    public EntityEffectBaseArgs(EntityUid targetEntity, IEntityManager entityManager, EntityUid? user = null)
    {
        TargetEntity = targetEntity;
        EntityManager = entityManager;
        User = user;
    }
}

/// <summary>
/// Reagent-specific args for the old Effect() API.
/// </summary>
public sealed class EntityEffectReagentArgs : EntityEffectBaseArgs
{
    public readonly FixedPoint2 Quantity;
    public readonly float Scale;

    public EntityEffectReagentArgs(EntityUid targetEntity, IEntityManager entityManager, EntityUid? user, FixedPoint2 quantity, float scale)
        : base(targetEntity, entityManager, user)
    {
        Quantity = quantity;
        Scale = scale;
    }
}

/// <summary>
/// A basic instantaneous effect which can be applied to an entity via events.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class EntityEffect
{
    // New event-based API (used by non-_Shitmed effects via EntityEffectBase<T>)
    public virtual void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser, float scale, EntityUid? user)
    {
        Effect(new EntityEffectBaseArgs(target, IoCManager.Resolve<IEntityManager>(), user));
    }

    // Old args-based API used by _Shitmed effects
    public virtual void Effect(EntityEffectBaseArgs args) { }

    [DataField]
    public EntityCondition[]? Conditions;

    /// <summary>
    /// If our scale is less than this value, the effect fails.
    /// </summary>
    [DataField]
    public virtual float MinScale { get; private set; }

    /// <summary>
    /// If true, then it allows the scale multiplier to go above 1.
    /// </summary>
    [DataField]
    public virtual bool Scaling { get; private set; } = true;

    // TODO: This should be an entity condition but guidebook relies on it heavily for formatting...
    /// <summary>
    /// Probability of the effect occuring.
    /// </summary>
    [DataField]
    public float Probability = 1.0f;

    public virtual string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    protected virtual string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys) => null;

    /// <summary>
    /// If this effect is logged, how important is the log?
    /// </summary>
    [ViewVariables]
    public virtual LogImpact? Impact => null;

    [ViewVariables]
    public virtual LogType LogType => LogType.EntityEffect;
}

/// <summary>
/// Used to store an <see cref="EntityEffect"/> so it can be raised without losing the type of the condition.
/// </summary>
/// <typeparam name="T">The Condition wer are raising.</typeparam>
public abstract partial class EntityEffectBase<T> : EntityEffect where T : EntityEffectBase<T>
{
    public override void RaiseEvent(EntityUid target, IEntityEffectRaiser raiser, float scale, EntityUid? user)
    {
        if (this is not T type)
            return;

        raiser.RaiseEffectEvent(target, type, scale, user);
    }
}
