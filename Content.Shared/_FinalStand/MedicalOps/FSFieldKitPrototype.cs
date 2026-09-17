using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

[Prototype("fsFieldKit")]
public sealed partial class FSFieldKitPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField(required: true)]
    public LocId Purpose { get; private set; }

    [DataField(required: true)]
    public LocId Delivery { get; private set; }

    [DataField]
    public ProtoId<ReactionPrototype>? Reaction { get; private set; }

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Ingredients { get; private set; } = new();

    [DataField]
    public List<ProtoId<ReagentPrototype>> Carried { get; private set; } = new();

    [DataField]
    public int Priority { get; private set; }

    /// <summary>Its missing ingredients come from research, not from a dispenser.</summary>
    [DataField]
    public bool Research { get; private set; }

    public Dictionary<string, FixedPoint2> ResolveIngredients(IPrototypeManager prototypes)
    {
        var resolved = new Dictionary<string, FixedPoint2>();

        if (Reaction is { } reaction && prototypes.TryIndex(reaction, out var reactionProto))
        {
            foreach (var (id, reactant) in reactionProto.Reactants)
                resolved[id] = reactant.Amount;

            return resolved;
        }

        foreach (var (id, amount) in Ingredients)
            resolved[id] = amount;

        return resolved;
    }
}
