using System.Globalization;

namespace Content.Shared._FinalStand.Perks;

public enum FSArchetypeTier : byte
{
    None,
    Archetype,
    Keystone,
}

public readonly record struct FSArchetypeBonus(float Damage, float DamageTaken, float Speed, float Credits)
{
    public static readonly FSArchetypeBonus None = new(1f, 1f, 1f, 1f);
}

public readonly record struct FSArchetype(PerkCategory Category, FSArchetypeTier Tier, int Count)
{
    public bool Active => Tier != FSArchetypeTier.None;
}

public static class FSArchetypeRules
{
    public const int ArchetypeCount = 3;
    public const int KeystoneCount = 5;

    public static FSArchetype Resolve(IEnumerable<PerkCategory> slotted)
    {
        var counts = new Dictionary<PerkCategory, int>();
        foreach (var category in slotted)
            counts[category] = counts.GetValueOrDefault(category) + 1;

        var best = default(FSArchetype);
        foreach (var (category, count) in counts)
        {
            if (count > best.Count || count == best.Count && category < best.Category)
                best = new FSArchetype(category, TierFor(count), count);
        }

        return best;
    }

    public static FSArchetype Resolve(IEnumerable<string> slottedIds, Func<string, int> level)
    {
        var categories = new List<PerkCategory>();
        foreach (var id in slottedIds)
        {
            if (!string.IsNullOrEmpty(id) && level(id) > 0 && FSPerkDef.All.TryGetValue(id, out var def))
                categories.Add(def.Category);
        }

        return Resolve(categories);
    }

    public static FSArchetypeTier TierFor(int count)
        => count >= KeystoneCount ? FSArchetypeTier.Keystone
            : count >= ArchetypeCount ? FSArchetypeTier.Archetype
            : FSArchetypeTier.None;

    public static FSArchetypeBonus Bonus(FSArchetype archetype)
    {
        var keystone = archetype.Tier == FSArchetypeTier.Keystone;
        return archetype.Tier == FSArchetypeTier.None ? FSArchetypeBonus.None : archetype.Category switch
        {
            PerkCategory.Red => new FSArchetypeBonus(
                keystone ? FSPerkBonusConstants.KeystoneSlayerDamage : FSPerkBonusConstants.ArchetypeSlayerDamage, 1f, 1f, 1f),
            PerkCategory.Blue => new FSArchetypeBonus(1f,
                keystone ? FSPerkBonusConstants.KeystoneBulwarkDamageTaken : FSPerkBonusConstants.ArchetypeBulwarkDamageTaken, 1f, 1f),
            PerkCategory.Green => new FSArchetypeBonus(1f, 1f,
                keystone ? FSPerkBonusConstants.KeystoneOperatorSpeed : FSPerkBonusConstants.ArchetypeOperatorSpeed, 1f),
            PerkCategory.Yellow => new FSArchetypeBonus(1f, 1f, 1f,
                keystone ? FSPerkBonusConstants.KeystoneTycoonCredits : FSPerkBonusConstants.ArchetypeTycoonCredits),
            PerkCategory.Purple => keystone
                ? new FSArchetypeBonus(FSPerkBonusConstants.KeystoneSpecialistBonus, FSPerkBonusConstants.KeystoneSpecialistDamageTaken,
                    FSPerkBonusConstants.KeystoneSpecialistSpeed, FSPerkBonusConstants.KeystoneSpecialistBonus)
                : new FSArchetypeBonus(FSPerkBonusConstants.ArchetypeSpecialistBonus, FSPerkBonusConstants.ArchetypeSpecialistDamageTaken,
                    FSPerkBonusConstants.ArchetypeSpecialistSpeed, FSPerkBonusConstants.ArchetypeSpecialistBonus),
            _ => FSArchetypeBonus.None,
        };
    }

    public static string NameLocId(PerkCategory category) => category switch
    {
        PerkCategory.Red => "fs-archetype-slayer",
        PerkCategory.Blue => "fs-archetype-bulwark",
        PerkCategory.Green => "fs-archetype-operator",
        PerkCategory.Yellow => "fs-archetype-tycoon",
        _ => "fs-archetype-specialist",
    };

    public static string EffectLocId(PerkCategory category) => category switch
    {
        PerkCategory.Red => "fs-archetype-slayer-effect",
        PerkCategory.Blue => "fs-archetype-bulwark-effect",
        PerkCategory.Green => "fs-archetype-operator-effect",
        PerkCategory.Yellow => "fs-archetype-tycoon-effect",
        _ => "fs-archetype-specialist-effect",
    };

    // Whole percentages for the effect line; the keystone quotes its bigger numbers through the same args.
    public static (string, object)[] EffectArgs(FSArchetypeBonus bonus) =>
    [
        ("damage", Percent(bonus.Damage - 1f)),
        ("resist", Percent(1f - bonus.DamageTaken)),
        ("speed", Percent(bonus.Speed - 1f)),
        ("credits", Percent(bonus.Credits - 1f)),
    ];

    // Not Convert: the client sandbox doesn't allow it.
    private static string Percent(float fraction)
        => ((double) fraction * 100).ToString("0.##", CultureInfo.InvariantCulture);
}
