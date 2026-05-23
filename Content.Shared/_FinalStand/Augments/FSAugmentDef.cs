using System.Linq;

namespace Content.Shared._FinalStand.Augments;

public enum AugmentCategory { Red, Blue, Green, Yellow, Purple }

public sealed class FSAugmentDef
{
    public string Id          { get; }
    public string Name        { get; }
    public string Description { get; }
    public AugmentCategory Category { get; }

    public string[] LevelEffects { get; }

    // todo: replace with rsi sprite paths when augment icons exist
    public string? IconRsi   { get; init; }
    public string? IconState { get; init; }

    public const int MaxLevel = 4;
    public const int SlotCount = 6;

    public static int CostForUpgrade(int currentLevel) => currentLevel + 1;

    public FSAugmentDef(string id, string name, string description,
        AugmentCategory category, string[] levelEffects)
    {
        if (levelEffects.Length != MaxLevel)
            throw new ArgumentException(
                $"FSAugmentDef '{id}': expected {MaxLevel} level effects, got {levelEffects.Length}. " +
                $"Every augment must have exactly one string per level.");
        if (description.Length > 90)
            throw new ArgumentException(
                $"FSAugmentDef '{id}': description is {description.Length} chars (max 90). " +
                $"Keep descriptions concise — the info panel has a fixed width.");

        Id = id; Name = name; Description = description;
        Category = category; LevelEffects = levelEffects;
    }

    public static readonly IReadOnlyDictionary<string, FSAugmentDef> All;

    static FSAugmentDef()
    {
        FSAugmentDef[] list =
        [
            new("StoppingPower", "Stopping Power",
                "Deal increased projectile damage. Does not apply to launchers.",
                AugmentCategory.Red,
                ["+4% Damage", "+8% Damage", "+12% Damage", "+16% Damage"]),

            new("BulletStorm", "Bullet Storm",
                "Increase fire rate on all firearms.",
                AugmentCategory.Red,
                ["+8% Fire Rate", "+16% Fire Rate", "+24% Fire Rate", "+32% Fire Rate"]),

            // todo: ironhide — mob threshold increase
            new("IronHide", "Iron Hide",
                "Permanently increase your maximum health.",
                AugmentCategory.Blue,
                ["+5 Max Health", "+10 Max Health", "+15 Max Health", "+20 Max Health"]),

            // todo: quickrecover — regen rate modifier
            new("QuickRecover", "Quick Recover",
                "Increase passive health regeneration rate.",
                AugmentCategory.Blue,
                ["+25% Regen Rate", "+50% Regen Rate", "+75% Regen Rate", "+100% Regen Rate"]),

            // todo: sprinter — movement speed modifier
            new("Sprinter", "Sprinter",
                "Move faster on the battlefield.",
                AugmentCategory.Green,
                ["+5% Move Speed", "+10% Move Speed", "+15% Move Speed", "+20% Move Speed"]),

            // todo: scavenger — starting credits bonus
            new("Scavenger", "Scavenger",
                "Start each round with bonus credits.",
                AugmentCategory.Green,
                ["+$50 Starting Credits", "+$100 Starting Credits",
                 "+$150 Starting Credits", "+$200 Starting Credits"]),

            // todo: profiteer — kill credit bonus
            new("Profiteer", "Profiteer",
                "Earn more credits for killing enemies.",
                AugmentCategory.Yellow,
                ["+5% Kill Credits", "+10% Kill Credits", "+15% Kill Credits", "+20% Kill Credits"]),

            // todo: fastlearner — xp multiplier
            new("FastLearner", "Fast Learner",
                "Gain experience faster from all sources.",
                AugmentCategory.Purple,
                ["+10% XP Gain", "+20% XP Gain", "+30% XP Gain", "+40% XP Gain"]),
        ];

        All = list.ToDictionary(a => a.Id);
    }
}
