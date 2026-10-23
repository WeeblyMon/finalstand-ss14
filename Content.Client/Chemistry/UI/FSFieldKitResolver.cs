using Content.Shared.Chemistry.Reaction;
using Robust.Shared.Prototypes;

namespace Content.Client.Chemistry.UI;

public static class FSFieldKitResolver
{
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
