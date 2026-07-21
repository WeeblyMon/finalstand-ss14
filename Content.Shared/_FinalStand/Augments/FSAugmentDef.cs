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

    public string? IconFile { get; init; }  // filename without extension; defaults to Id.ToLowerInvariant()

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
                "Increases your melee damage and damage resistance while wielding a melee weapon.",
                AugmentCategory.Purple,
                ["+5% Damage / +12% Resistance", "+10% Damage / +24% Resistance",
                 "+15% Damage / +36% Resistance", "+20% Damage / +48% Resistance"]),

            // ── Kill-stack augments ──────────────────────────────────────────────
            new("DeathAura", "Death Aura",
                "Kills grant stacks, increasing your damage by 1% per stack. Lose all stacks after 8s.",
                AugmentCategory.Red,
                ["+5 Max Stacks", "+10 Max Stacks", "+15 Max Stacks", "+20 Max Stacks"]),

            new("Adrenaline", "Adrenaline",
                "Killing an enemy grants unlimited stamina for a few seconds.",
                AugmentCategory.Green,
                ["+2.1s Duration", "+2.8s Duration", "+3.5s Duration", "+4.2s Duration"]),

            new("SpeedDemon", "Speed Demon",
                "Kills increase your movement speed up to 7 stacks. Lose 1 stack/s after 5s.",
                AugmentCategory.Green,
                ["+1% Speed/Stack", "+2% Speed/Stack", "+3% Speed/Stack", "+4% Speed/Stack"]),

            new("Rampage", "Rampage",
                "Melee kills increase resistance, health regen, and speed. 5 stacks max.",
                AugmentCategory.Purple,
                ["+3% Resist/+0.2 Regen/+1% Speed", "+6%/+0.4/+2%",
                 "+9%/+0.6/+3%", "+12%/+0.8/+4%"]),

            // ── Economy augments ────────────────────────────────────────────────
            new("Investor", "Investor",
                "At the end of each wave your money gains interest.",
                AugmentCategory.Yellow,
                ["+2.5% Return", "+5% Return", "+7.5% Return", "+10% Return"]),

            new("MutualFund", "Mutual Fund",
                "At the end of each wave your team's money gains interest.",
                AugmentCategory.Yellow,
                ["+1.25% Team Return", "+2.5% Team Return", "+3.75% Team Return", "+5% Team Return"]),

            // ── Defensive / utility augments ────────────────────────────────────
            new("Untouchable", "Untouchable",
                "Automatically blocks one incoming hit. Charges refill after 30 seconds.",
                AugmentCategory.Blue,
                ["+1 Max Charge", "+2 Max Charges", "+3 Max Charges", "+4 Max Charges"]),

            new("Martyr", "Martyr",
                "Going down triggers an explosion at your feet.",
                AugmentCategory.Red,
                ["Small Explosion", "Medium Explosion", "Large Explosion", "Max Explosion"]),

            new("GlassCannon", "Glass Cannon",
                "Take 100% more damage, but deal more damage.",
                AugmentCategory.Red,
                ["+7% Damage", "+14% Damage", "+21% Damage", "+28% Damage"]),

            new("Pacifist", "Pacifist",
                "Deal 25% less damage, but gain significant damage resistance.",
                AugmentCategory.Blue,
                ["+40% Resistance", "+60% Resistance", "+80% Resistance", "+100% Resistance"]),

            new("FieldMedic", "Field Medic",
                "Increases the potency of your healing.",
                AugmentCategory.Blue,
                ["+15% Healing", "+30% Healing", "+45% Healing", "+60% Healing"]),

            new("Cargonian", "Cargonian",
                "Reduces the movement speed penalty from dragging bodies.",
                AugmentCategory.Green,
                ["-33% Drag Penalty", "-67% Drag Penalty", "-100% Drag Penalty", "-100% Drag Penalty"]),

            // ── Weapon augments ─────────────────────────────────────────────────
            new("LegBreaker", "Leg Breaker",
                "Critical hits slow enemies.",
                AugmentCategory.Blue,
                ["-10% Enemy Speed on Crit", "-20%", "-30%", "-40%"]),

            new("BackBreaker", "Back Breaker",
                "Critical shots knock enemies back.",
                AugmentCategory.Green,
                ["+3 Knockback", "+6 Knockback", "+9 Knockback", "+12 Knockback"]),

            new("KnockbackBlast", "Knockback Blast",
                "Your shotgun shots knock enemies back.",
                AugmentCategory.Blue,
                ["+3 Knockback", "+6 Knockback", "+9 Knockback", "+12 Knockback"]),

            new("DeepImpact", "Deep Impact",
                "Your shots pierce through enemies.",
                AugmentCategory.Red,
                ["+1 Pierce", "+2 Pierce", "+3 Pierce", "+4 Pierce"]),

            new("Officer", "Officer",
                "Using a whistle near allies increases their damage for 8 seconds.",
                AugmentCategory.Green,
                ["+15% Ally Damage", "+30% Ally Damage", "+45% Ally Damage", "+45% Ally Damage"]),
        ];

        All = list.ToDictionary(a => a.Id);
    }
}
