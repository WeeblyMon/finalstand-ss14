using System.Linq;

namespace Content.Shared._FinalStand.Perks;

public enum PerkCategory { Red, Blue, Green, Yellow, Purple }

public sealed class FSPerkDef
{
    public string Id          { get; }
    public string Name        { get; }
    public string Description { get; }
    public PerkCategory Category { get; }

    public string[] LevelEffects { get; }

    public string? IconFile { get; init; }  // filename without extension; defaults to Id.ToLowerInvariant()

    public const int MaxLevel = 4;
    public const int SlotCount = 6;

    public static int CostForUpgrade(int currentLevel) => currentLevel + 1;

    public FSPerkDef(string id, string name, string description,
        PerkCategory category, string[] levelEffects)
    {
        if (levelEffects.Length != MaxLevel)
            throw new ArgumentException(
                $"FSPerkDef '{id}': expected {MaxLevel} level effects, got {levelEffects.Length}. " +
                $"Every perk must have exactly one string per level.");
        if (description.Length > 90)
            throw new ArgumentException(
                $"FSPerkDef '{id}': description is {description.Length} chars (max 90). " +
                $"Keep descriptions concise — the info panel has a fixed width.");

        Id = id; Name = name; Description = description;
        Category = category; LevelEffects = levelEffects;
    }

    public static readonly IReadOnlyDictionary<string, FSPerkDef> All;

    static FSPerkDef()
    {
        FSPerkDef[] list =
        [
            new("StoppingPower", "Stopping Power",
                "Deal increased projectile damage. Does not apply to launchers.",
                PerkCategory.Red,
                ["+4% Damage", "+8% Damage", "+12% Damage", "+16% Damage"]),

            new("BulletStorm", "Bullet Storm",
                "Increase fire rate on all firearms.",
                PerkCategory.Red,
                ["+8% Fire Rate", "+16% Fire Rate", "+24% Fire Rate", "+32% Fire Rate"]),

            new("Juggernaught", "Juggernaught",
                "Take less damage from zombies.",
                PerkCategory.Blue,
                ["+15% Resistance", "+30% Resistance", "+45% Resistance", "+60% Resistance"]),

            new("Lightweight", "Lightweight",
                "Increases your movement speed.",
                PerkCategory.Green,
                ["+3% Speed", "+6% Speed", "+9% Speed", "+12% Speed"]),

            new("Profiteer", "Profiteer",
                "Increases the amount of money you earn.",
                PerkCategory.Yellow,
                ["+7% Money", "+14% Money", "+21% Money", "+28% Money"]),

            new("SwordAndShield", "Sword and Shield",
                "Increases your melee damage and damage resistance while wielding a melee weapon.",
                PerkCategory.Purple,
                ["+5% Damage / +12% Resistance", "+10% Damage / +24% Resistance",
                 "+15% Damage / +36% Resistance", "+20% Damage / +48% Resistance"]),

            // ── Kill-stack perks ──────────────────────────────────────────────
            new("DeathAura", "Death Aura",
                "Kills grant stacks, increasing your damage by 2% per stack. Lose all stacks after 8s.",
                PerkCategory.Red,
                ["+5 Max Stacks", "+10 Max Stacks", "+15 Max Stacks", "+20 Max Stacks"]),

            new("Adrenaline", "Adrenaline",
                "Killing an enemy grants unlimited stamina for a few seconds.",
                PerkCategory.Green,
                ["+2.1s Duration", "+2.8s Duration", "+3.5s Duration", "+4.2s Duration"]),

            new("SpeedDemon", "Speed Demon",
                "Kills increase your movement speed up to 7 stacks. Lose 1 stack/s after 5s.",
                PerkCategory.Green,
                ["+1% Speed/Stack", "+2% Speed/Stack", "+3% Speed/Stack", "+4% Speed/Stack"]),

            new("Rampage", "Rampage",
                "Melee kills increase resistance, health regen, and speed. 5 stacks max.",
                PerkCategory.Purple,
                ["+3% Resist/+0.2 Regen/+1% Speed", "+6%/+0.4/+2%",
                 "+9%/+0.6/+3%", "+12%/+0.8/+4%"]),

            // ── Economy perks ────────────────────────────────────────────────
            new("Investor", "Investor",
                "At the end of each wave your money gains interest.",
                PerkCategory.Yellow,
                ["+2.5% Return", "+5% Return", "+7.5% Return", "+10% Return"]),

            new("MutualFund", "Mutual Fund",
                "At the end of each wave your team's money gains interest.",
                PerkCategory.Yellow,
                ["+1.25% Team Return", "+2.5% Team Return", "+3.75% Team Return", "+5% Team Return"]),

            // ── Defensive / utility perks ────────────────────────────────────
            new("Untouchable", "Untouchable",
                "Automatically blocks one incoming hit. Charges refill after 30 seconds.",
                PerkCategory.Blue,
                ["+1 Max Charge", "+2 Max Charges", "+3 Max Charges", "+4 Max Charges"]),

            new("Martyr", "Martyr",
                "Going down triggers an explosion at your feet.",
                PerkCategory.Red,
                ["Small Explosion", "Medium Explosion", "Large Explosion", "Max Explosion"]),

            new("GlassCannon", "Glass Cannon",
                "Take 100% more damage, but deal more damage.",
                PerkCategory.Red,
                ["+7% Damage", "+14% Damage", "+21% Damage", "+28% Damage"]),

            new("Pacifist", "Pacifist",
                "Deal 25% less damage, but gain significant damage resistance.",
                PerkCategory.Blue,
                ["+20% Resistance", "+40% Resistance", "+60% Resistance", "+80% Resistance"]),

            new("FieldMedic", "Field Medic",
                "Increases the potency of your healing.",
                PerkCategory.Blue,
                ["+15% Healing", "+30% Healing", "+45% Healing", "+60% Healing"]),

            new("Cargonian", "Cargonian",
                "Reduces the movement speed penalty from dragging bodies.",
                PerkCategory.Green,
                ["-33% Drag Penalty", "-67% Drag Penalty", "-100% Drag Penalty", "-100% Drag Penalty"]),

            // ── Weapon perks ─────────────────────────────────────────────────
            new("LegBreaker", "Leg Breaker",
                "Critical hits stagger enemies with stamina damage.",
                PerkCategory.Blue,
                ["25 Stamina Damage on Crit", "50 Stamina Damage on Crit", "75 Stamina Damage on Crit", "100 Stamina Damage on Crit"]),

            new("BackBreaker", "Back Breaker",
                "Critical shots knock enemies back.",
                PerkCategory.Green,
                ["+3 Knockback", "+6 Knockback", "+9 Knockback", "+12 Knockback"]),

            new("KnockbackBlast", "Knockback Blast",
                "Your shotgun shots knock enemies back.",
                PerkCategory.Blue,
                ["+3 Knockback", "+6 Knockback", "+9 Knockback", "+12 Knockback"]),

            new("DeepImpact", "Deep Impact",
                "Your shots pierce through enemies.",
                PerkCategory.Red,
                ["+1 Pierce", "+2 Pierce", "+3 Pierce", "+4 Pierce"]),

            new("Officer", "Officer",
                "Using a whistle near allies increases their damage for 8 seconds.",
                PerkCategory.Green,
                ["+15% Ally Damage", "+30% Ally Damage", "+45% Ally Damage", "+60% Ally Damage"]),

            new("HarvesterTuning", "Harvester Tuning",
                "Increases research points gained per Harvester hit.",
                PerkCategory.Yellow,
                ["+1 RP/Hit", "+2 RP/Hit", "+3 RP/Hit", "+4 RP/Hit"]),
        ];

        All = list.ToDictionary(a => a.Id);
    }
}
