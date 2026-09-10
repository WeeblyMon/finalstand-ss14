using System.Collections.Generic;
using System.Linq;
using Content.Client.Stylesheets;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client.Chemistry.UI;

public static class FSRecipeHints
{
    private const int MaxReady = 8;
    private const int MaxNearMisses = 6;

    public static string KeyFor(IEnumerable<string> held)
    {
        return string.Join('|', held.OrderBy(x => x));
    }

    public static void Populate(
        BoxContainer target,
        IPrototypeManager prototypes,
        IReadOnlyDictionary<string, FixedPoint2> held)
    {
        target.Children.Clear();

        if (held.Count == 0)
        {
            target.AddChild(Weak(Loc.GetString("chem-master-window-recipes-empty")));
            return;
        }

        var ready = new List<string>();
        var nearMisses = new List<string>();

        foreach (var reaction in prototypes.EnumeratePrototypes<ReactionPrototype>())
        {
            if (reaction.Reactants.Count == 0)
                continue;

            var matched = 0;
            string? missing = null;

            foreach (var (reactantId, reactant) in reaction.Reactants)
            {
                if (held.TryGetValue(reactantId, out var have) && have >= reactant.Amount)
                {
                    matched++;
                    continue;
                }

                if (missing != null)
                {
                    missing = null;
                    break;
                }

                prototypes.TryIndex(reactantId, out ReagentPrototype? missingProto);
                missing = missingProto?.LocalizedName ?? reactantId;
            }

            if (matched == reaction.Reactants.Count)
                ready.Add(NameOf(reaction, prototypes));
            else if (missing != null && matched > 0)
                nearMisses.Add(Loc.GetString("chem-master-window-recipe-needs",
                    ("recipe", NameOf(reaction, prototypes)), ("reagent", missing)));
        }

        if (ready.Count == 0 && nearMisses.Count == 0)
        {
            target.AddChild(Weak(Loc.GetString("chem-master-window-recipes-none")));
            return;
        }

        if (ready.Count > 0)
        {
            target.AddChild(new Label
            {
                Text = Loc.GetString("chem-master-window-recipes-ready",
                    ("recipes", string.Join(", ", ready.OrderBy(x => x).Take(MaxReady)))),
                Modulate = Color.FromHex("#4FBF7A"),
            });
        }

        foreach (var miss in nearMisses.OrderBy(x => x).Take(MaxNearMisses))
            target.AddChild(Weak(miss));
    }

    private static Label Weak(string text)
    {
        return new Label
        {
            Text = text,
            StyleClasses = { StyleClass.LabelWeak },
        };
    }

    private static string NameOf(ReactionPrototype reaction, IPrototypeManager prototypes)
    {
        if (!string.IsNullOrEmpty(reaction.Name))
            return Loc.GetString(reaction.Name);

        foreach (var product in reaction.Products.Keys)
        {
            if (prototypes.TryIndex(product, out ReagentPrototype? proto))
                return proto.LocalizedName;
        }

        return reaction.ID;
    }
}
