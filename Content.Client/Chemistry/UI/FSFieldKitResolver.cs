using System.Linq;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Client.Chemistry.UI;

public sealed class FSFieldKitPlan
{
    public readonly Dictionary<string, FixedPoint2> Base = new();
    public readonly List<ReactionPrototype> Steps = new();
    public readonly HashSet<string> Unobtainable = new();
}

public static class FSFieldKitResolver
{
    private const int MaxDepth = 8;

    private static Dictionary<string, ReactionPrototype>? _producedBy;
    private static int _protoVersion = -1;

    public static void Invalidate()
    {
        _producedBy = null;
    }

    private static Dictionary<string, ReactionPrototype> ProducedBy(IPrototypeManager prototypes)
    {
        if (_producedBy != null)
            return _producedBy;

        var map = new Dictionary<string, ReactionPrototype>();

        foreach (var reaction in prototypes.EnumeratePrototypes<ReactionPrototype>())
        {
            foreach (var product in reaction.Products.Keys)
            {
                if (map.TryGetValue(product, out var existing) && existing.Reactants.Count <= reaction.Reactants.Count)
                    continue;

                map[product] = reaction;
            }
        }

        _producedBy = map;
        return map;
    }

    public static FSFieldKitPlan Plan(
        IPrototypeManager prototypes,
        IReadOnlyDictionary<string, FixedPoint2> wanted,
        IReadOnlySet<string> sourceable)
    {
        var plan = new FSFieldKitPlan();
        var producedBy = ProducedBy(prototypes);
        var onPath = new HashSet<string>();

        foreach (var (reagent, amount) in wanted)
            Walk(reagent, amount, 0, plan, producedBy, sourceable, onPath);

        return plan;
    }

    private static void Walk(
        string reagent,
        FixedPoint2 need,
        int depth,
        FSFieldKitPlan plan,
        IReadOnlyDictionary<string, ReactionPrototype> producedBy,
        IReadOnlySet<string> sourceable,
        HashSet<string> onPath)
    {
        if (sourceable.Contains(reagent))
        {
            plan.Base[reagent] = plan.Base.GetValueOrDefault(reagent, FixedPoint2.Zero) + need;
            return;
        }

        if (depth >= MaxDepth || !onPath.Add(reagent) || !producedBy.TryGetValue(reagent, out var reaction))
        {
            plan.Unobtainable.Add(reagent);
            return;
        }

        var yield = reaction.Products.GetValueOrDefault(reagent, FixedPoint2.New(1));
        var batches = yield <= FixedPoint2.Zero
            ? 1
            : Math.Max(1, (int) Math.Ceiling(need.Float() / yield.Float()));

        foreach (var (reactantId, reactant) in reaction.Reactants)
            Walk(reactantId, reactant.Amount * batches, depth + 1, plan, producedBy, sourceable, onPath);

        onPath.Remove(reagent);

        if (!plan.Steps.Contains(reaction))
            plan.Steps.Add(reaction);
    }

    public static string NameOf(ReactionPrototype reaction, IPrototypeManager prototypes)
    {
        if (!string.IsNullOrEmpty(reaction.Name))
            return Loc.GetString(reaction.Name);

        foreach (var product in reaction.Products.Keys)
        {
            if (prototypes.TryIndex(product, out Shared.Chemistry.Reagent.ReagentPrototype? proto))
                return proto.LocalizedName;
        }

        return reaction.ID;
    }
}
