using Content.Client._FinalStand.Stylesheets;
using Content.Shared._FinalStand.Perks;

namespace Content.Client._FinalStand.Perks.UI;

public sealed partial class PerkShopWindow
{
    private void RefreshArchetype(FSPerksStateEvent ev)
    {
        var archetype = FSArchetypeRules.Resolve(ev.Slots, id => ev.Levels.GetValueOrDefault(id));
        ArchetypeLabel.FontOverride = _fontSmallBold;
        ArchetypeLabel.ToolTip = Loc.GetString("fs-archetype-tooltip",
            ("archetype", FSArchetypeRules.ArchetypeCount), ("keystone", FSArchetypeRules.KeystoneCount));

        if (!archetype.Active)
        {
            ArchetypeLabel.Text = archetype.Count > 0
                ? Loc.GetString("fs-archetype-progress", ("category", CatLabel[archetype.Category]),
                    ("count", archetype.Count), ("needed", FSArchetypeRules.ArchetypeCount))
                : string.Empty;
            ArchetypeLabel.FontColorOverride = FSUiPalette.TextMuted;
            return;
        }

        var name = Loc.GetString(FSArchetypeRules.NameLocId(archetype.Category));
        var effect = Loc.GetString(FSArchetypeRules.EffectLocId(archetype));
        ArchetypeLabel.Text = archetype.Tier == FSArchetypeTier.Keystone
            ? Loc.GetString("fs-archetype-keystone-line", ("name", name), ("effect", effect))
            : Loc.GetString("fs-archetype-line", ("name", name), ("effect", effect),
                ("count", archetype.Count), ("needed", FSArchetypeRules.KeystoneCount));
        ArchetypeLabel.FontColorOverride = FSPerkPalette.Accent[archetype.Category];
    }
}
