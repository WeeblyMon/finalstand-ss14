using Content.Shared._FinalStand.Titles;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Perks;

public enum FSMasteryTier : byte
{
    None,
    Bronze,
    Silver,
    Gold,
}

public static class FSPerkMastery
{
    public static readonly int[] Thresholds = { 500, 2500, 10000 };

    public static FSMasteryTier Tier(int kills)
    {
        var tier = FSMasteryTier.None;
        for (var i = 0; i < Thresholds.Length; i++)
        {
            if (kills >= Thresholds[i])
                tier = (FSMasteryTier) (i + 1);
        }
        return tier;
    }

    public static int? NextThreshold(int kills)
    {
        foreach (var threshold in Thresholds)
        {
            if (kills < threshold)
                return threshold;
        }
        return null;
    }

    public static string TierLocId(FSMasteryTier tier) => tier switch
    {
        FSMasteryTier.Bronze => "fs-mastery-bronze",
        FSMasteryTier.Silver => "fs-mastery-silver",
        FSMasteryTier.Gold => "fs-mastery-gold",
        _ => "fs-mastery-none",
    };

    public static ProtoId<FSTitlePrototype> CategoryTitle(PerkCategory category) => category switch
    {
        PerkCategory.Red => "FSTitleMasteryRed",
        PerkCategory.Blue => "FSTitleMasteryBlue",
        PerkCategory.Green => "FSTitleMasteryGreen",
        PerkCategory.Yellow => "FSTitleMasteryYellow",
        _ => "FSTitleMasteryPurple",
    };
}
