using System.Globalization;
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

    // Level text is generated from FSPerkBonusConstants so the shop can never advertise a rate the
    // maths does not pay. Pct scales by 100, Flat does not.
    private static string[] Pct(string format, params float[] rates) => Rows(format, 100f, rates);

    private static string[] Flat(string format, params float[] amounts) => Rows(format, 1f, amounts);

    // For perks that don't scale linearly, so the table itself is the source of truth.
    private static string[] Table(string format, float[] perLevel)
    {
        if (perLevel.Length != MaxLevel)
            throw new ArgumentException($"Perk table needs {MaxLevel} entries, got {perLevel.Length}.");

        return perLevel
            .Select(v => string.Format(CultureInfo.InvariantCulture, format,
                v.ToString("0.##", CultureInfo.InvariantCulture)))
            .ToArray();
    }

    private static string[] Rows(string format, float scale, float[] perLevel)
    {
        var rows = new string[MaxLevel];
        for (var level = 1; level <= MaxLevel; level++)
        {
            var args = new object[perLevel.Length];
            for (var i = 0; i < perLevel.Length; i++)
                args[i] = (perLevel[i] * level * scale).ToString("0.##", CultureInfo.InvariantCulture);

            rows[level - 1] = string.Format(CultureInfo.InvariantCulture, format, args);
        }

        return rows;
    }

    public static readonly IReadOnlyDictionary<string, FSPerkDef> All;

    static FSPerkDef()
    {
        FSPerkDef[] list =
        [
            new("StoppingPower", "Stopping Power",
                "Deal increased projectile damage. Does not apply to launchers.",
                PerkCategory.Red,
                Pct("+{0}% Damage", FSPerkBonusConstants.StoppingPowerPerLevel)),

            new("BulletStorm", "Bullet Storm",
                "Increase fire rate on all firearms.",
                PerkCategory.Red,
                Pct("+{0}% Fire Rate", FSPerkBonusConstants.BulletStormPerLevel)),

            new("Juggernaught", "Juggernaught",
                "Take less damage from zombies.",
                PerkCategory.Blue,
                Pct("+{0}% Resistance", FSPerkBonusConstants.JuggernaughtPerLevel)),

            new("Lightweight", "Lightweight",
                "Increases your movement speed.",
                PerkCategory.Green,
                Pct("+{0}% Speed", FSPerkBonusConstants.LightweightPerLevel)),

            new("Profiteer", "Profiteer",
                "Increases the amount of money you earn.",
                PerkCategory.Yellow,
                Pct("+{0}% Money", FSPerkBonusConstants.ProfiteerFraction)),

            new("SwordAndShield", "Sword and Shield",
                "Increases your melee damage and damage resistance while wielding a melee weapon.",
                PerkCategory.Purple,
                Pct("+{0}% Damage / +{1}% Resistance", FSPerkBonusConstants.SwordAndShieldPerLevel, FSPerkBonusConstants.SwordAndShieldResistPerLevel)),

            new("DeathAura", "Death Aura",
                "Kills grant stacks, increasing your damage by 2% per stack. Lose all stacks after 8s.",
                PerkCategory.Red,
                Flat("+{0} Max Stacks", FSPerkBonusConstants.DeathAuraStacksPerLevel)),

            new("Adrenaline", "Adrenaline",
                "Killing an enemy grants unlimited stamina for a few seconds.",
                PerkCategory.Green,
                Table("+{0}s Duration", FSPerkBonusConstants.AdrenalineSeconds)),

            new("SpeedDemon", "Speed Demon",
                "Kills increase your movement speed up to 7 stacks. Lose 1 stack/s after 5s.",
                PerkCategory.Green,
                Pct("+{0}% Speed/Stack", FSPerkBonusConstants.SpeedDemonPerLevel)),

            new("Rampage", "Rampage",
                "Melee kills increase resistance, health regen, and speed. 5 stacks max.",
                PerkCategory.Purple,
                ["+3% Resist/+0.2 Regen/+2% Speed", "+6%/+0.4/+4%",
                 "+9%/+0.6/+6%", "+12%/+0.8/+8%"]),

            new("Investor", "Investor",
                "At the end of each wave your money gains interest.",
                PerkCategory.Yellow,
                Pct("+{0}% Return", FSPerkBonusConstants.InvestorPerLevel)),

            new("MutualFund", "Mutual Fund",
                "At the end of each wave your team's money gains interest.",
                PerkCategory.Yellow,
                Pct("+{0}% Team Return", FSPerkBonusConstants.MutualFundPerLevel)),

            new("LifeLeech", "Life Leech",
                "Regenerate health after every zombie you kill.",
                PerkCategory.Blue,
                Table("+{0} Health", FSPerkBonusConstants.LifeLeechHeal)),

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
                Pct("+{0}% Damage", FSPerkBonusConstants.GlassCannonPerLevel)),

            new("Pacifist", "Pacifist",
                "Deal 25% less damage, but gain significant damage resistance.",
                PerkCategory.Blue,
                Pct("+{0}% Resistance", FSPerkBonusConstants.PacifistResistPerLevel)),

            new("FieldMedic", "Field Medic",
                "Increases the potency of your healing.",
                PerkCategory.Blue,
                Pct("+{0}% Healing", FSPerkBonusConstants.FieldMedicPerLevel)),

            new("Cargonian", "Cargonian",
                "Reduces the movement speed penalty from dragging bodies.",
                PerkCategory.Green,
                ["-33% Drag Penalty", "-67% Drag Penalty", "-100% Drag Penalty", "-100% Drag Penalty"]),

            new("LegBreaker", "Leg Breaker",
                "Critical hits stagger enemies with stamina damage.",
                PerkCategory.Blue,
                Flat("{0} Stamina Damage on Crit", FSPerkBonusConstants.LegBreakerStaminaPerLevel)),

            new("BackBreaker", "Back Breaker",
                "Critical shots knock enemies back.",
                PerkCategory.Green,
                ["+3 Knockback", "+6 Knockback", "+9 Knockback", "+12 Knockback"]),

            new("KnockbackBlast", "Knockback Blast",
                "Your shotgun shots knock enemies back.",
                PerkCategory.Blue,
                ["+3 Knockback", "+6 Knockback", "+9 Knockback", "+12 Knockback"]),

            new("DeepImpact", "Deep Impact",
                "Your shots pierce through enemies, halving damage with each one.",
                PerkCategory.Red,
                ["+1 Pierce", "+2 Pierce", "+3 Pierce", "+4 Pierce"]),

            new("Officer", "Officer",
                "Using a whistle near allies increases their damage for 8 seconds.",
                PerkCategory.Green,
                Pct("+{0}% Ally Damage", FSPerkBonusConstants.OfficerBuffPerLevel)),

            new("HarvesterTuning", "Harvester Tuning",
                "Increases research points gained per Harvester hit.",
                PerkCategory.Yellow,
                ["+1 RP/Hit", "+2 RP/Hit", "+3 RP/Hit", "+4 RP/Hit"]),
        ];

        All = list.ToDictionary(a => a.Id);
    }
}
