using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Solution;

/// <summary>
/// Adjust a reagent in this solution by an amount modified by scale.
/// Quantity is modified by scale.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class AdjustReagentEntityEffectSystem : EntityEffectSystem<SolutionComponent, AdjustReagent>
{
    [Dependency] private SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;

    protected override void Effect(Entity<SolutionComponent> entity, ref EntityEffectEvent<AdjustReagent> args)
    {
        if (args.Effect.Reagent is not { } reagent)
        {
            // Group-based removal: remove reagents belonging to the specified metabolizer group.
            // Each tick removes abs(amount * scale) units from every matching reagent.
            if (args.Effect.Group is not { } group)
                return;

            var removePerReagent = -(args.Effect.Amount * args.Scale);
            if (removePerReagent <= FixedPoint2.Zero)
                return;

            var toRemove = new List<(ReagentId Id, FixedPoint2 Qty)>();
            foreach (var rq in entity.Comp.Solution)
            {
                if (_protoManager.TryIndex<ReagentPrototype>(rq.Reagent.Prototype, out var proto)
                    && proto.Group == group)
                {
                    toRemove.Add((rq.Reagent, FixedPoint2.Min(removePerReagent, rq.Quantity)));
                }
            }

            foreach (var (id, qty) in toRemove)
                _solutionContainer.RemoveReagent(entity, id, qty);

            return;
        }

        var quantity = args.Effect.Amount * args.Scale;

        if (quantity > 0)
            _solutionContainer.TryAddReagent(entity, reagent, quantity);
        else
            _solutionContainer.RemoveReagent(entity, reagent, -quantity);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class AdjustReagent : EntityEffectBase<AdjustReagent>
{
    /// <summary>
    ///     The reagent ID to add or remove.
    /// </summary>
    [DataField]
    public ProtoId<ReagentPrototype>? Reagent;

    /// <summary>
    ///     Goob extension: remove all reagents belonging to a metabolizer group (e.g. "Medicine").
    ///     STUB — field exists for YAML compatibility; group-based removal is not implemented.
    /// </summary>
    [DataField]
    public string? Group;

    [DataField(required: true)]
    public FixedPoint2 Amount;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        if (Reagent is not { } reagent)
            return null;

        return prototype.Resolve(reagent, out ReagentPrototype? proto)
            ? Loc.GetString("entity-effect-guidebook-adjust-reagent-reagent",
                ("chance", Probability),
                ("deltasign", MathF.Sign(Amount.Float())),
                ("reagent", proto.LocalizedName),
                ("amount", MathF.Abs(Amount.Float())))
            : null;
    }
}
