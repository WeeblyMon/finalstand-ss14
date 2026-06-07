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

            new("Juggernaught", "Juggernaught",
                "Take less damage from zombies.",
                AugmentCategory.Blue,
                ["+15% Resistance", "+30% Resistance", "+45% Resistance", "+60% Resistance"]),

            new("Lightweight", "Lightweight",
                "Increases your movement speed.",
                AugmentCategory.Green,
                ["+3% Speed", "+6% Speed", "+9% Speed", "+12% Speed"]),

            new("Profiteer", "Profiteer",
                "Increases the amount of money you earn.",
                AugmentCategory.Yellow,
                ["+7% Money", "+14% Money", "+21% Money", "+28% Money"]),

            new("SwordAndShield", "Sword and Shield",
                "Increases your melee damage and resistance.",
                AugmentCategory.Purple,
                ["+5% Damage / +12% Resistance", "+10% Damage / +24% Resistance",
                 "+15% Damage / +36% Resistance", "+20% Damage / +48% Resistance"]),
        ];

        All = list.ToDictionary(a => a.Id);
    }
}
