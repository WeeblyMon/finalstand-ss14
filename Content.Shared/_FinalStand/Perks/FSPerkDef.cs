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

    private static string[] Levels(string format, Func<int, object[]> args)
        => Enumerable.Range(0, MaxLevel)
            .Select(i => string.Format(CultureInfo.InvariantCulture, format, args(i)))
            .ToArray();

    private static string[] PctTable(string format, float[] perLevel) =>
        Table(format, perLevel.Select(v => v * 100f).ToArray());

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

            new("CombatMedic", "Combat Medic",
                "Healing an ally grants you both increased damage for 15 seconds.",
                PerkCategory.Blue,
                Pct("+{0}% Damage", FSPerkBonusConstants.CombatMedicPerLevel)),

            new("Ravage", "Ravage",
                "Increases critical hit chance and attack speed with melee weapons.",
                PerkCategory.Red,
                Pct("+{0}% Crit Chance / +{1}% Attack Speed", FSPerkBonusConstants.RavageCritPerLevel, FSPerkBonusConstants.RavageAttackSpeedPerLevel)),

            new("CriticalPlus", "Critical Plus",
                "Increases your critical hit damage multiplier.",
                PerkCategory.Red,
                Pct("+{0}% Crit Damage", FSPerkBonusConstants.CriticalPlusPerLevel)),

            new("Implosion", "Implosion",
                "Increases your explosive damage, but your explosions are 25% smaller.",
                PerkCategory.Red,
                PctTable("+{0}% Explosive Damage", FSPerkBonusConstants.ImplosionDamage)),

            new("Rifleman", "Rifleman",
                "Increases crit chance and crit damage with the Mosin, Estoc and Hristov.",
                PerkCategory.Red,
                Pct("+{0}% Crit Chance/Damage", FSPerkBonusConstants.RiflemanPerLevel)),

            new("Berserker", "Berserker",
                "The lower your health, the more damage you deal. Halved for ranged damage.",
                PerkCategory.Red,
                Pct("Up to +{0}% Damage", FSPerkBonusConstants.BerserkerPerLevel)),

            new("Undying", "Undying",
                "Going down lets you keep fighting with unlimited ammo, then you die.",
                PerkCategory.Blue,
                Table("{0}s Duration", FSPerkBonusConstants.UndyingSeconds)),

            new("ManOnFire", "The Man On Fire",
                "Reduces fire damage taken. At max level, burning slowly heals you.",
                PerkCategory.Blue,
                PctTable("-{0}% Fire Damage", FSPerkBonusConstants.ManOnFireResist)),

            new("Bandolier", "Bandolier",
                "Increases the magazine size of your guns.",
                PerkCategory.Green,
                Pct("+{0}% Magazine Size", FSPerkBonusConstants.BandolierPerLevel)),

            new("ShadowRounds", "Shadow Rounds",
                "Shots have a chance to not consume ammo.",
                PerkCategory.Red,
                PctTable("{0}% Chance", FSPerkBonusConstants.ShadowRoundsChance)),

            new("Bloodload", "Bloodload",
                "Melee kills increase your reload speed for 12 seconds.",
                PerkCategory.Green,
                Pct("+{0}% Reload Speed", FSPerkBonusConstants.BloodloadPerLevel)),

            new("Speedload", "Speedload",
                "Increases your reload speed.",
                PerkCategory.Red,
                Pct("+{0}% Reload Speed", FSPerkBonusConstants.SpeedloadPerLevel)) { IconFile = "bloodload" },

            new("Executioner", "Executioner",
                "Deal increased damage to zombies below 35% health.",
                PerkCategory.Red,
                Pct("+{0}% Damage", FSPerkBonusConstants.ExecutionerPerLevel)) { IconFile = "berserker" },

            new("Shredder", "Shredder",
                "Hits stack vulnerability on zombies, up to 10. Stacks fade 4s after the last hit.",
                PerkCategory.Red,
                PctTable("+{0}% Damage Taken per Stack", FSPerkBonusConstants.ShredderPerStack)) { IconFile = "deathaura" },

            new("SpecialisedKilling", "Specialised Killing",
                "Deal increased damage to special zombies and bosses.",
                PerkCategory.Red,
                Pct("+{0}% Damage", FSPerkBonusConstants.SpecialisedKillingPerLevel)) { IconFile = "rifleman" },

            new("Thorns", "Thorns",
                "Kills grant stacks. Taking zombie damage spends one and reflects it back.",
                PerkCategory.Blue,
                Levels("{0} Stacks / {1:0.##}% Reflect",
                    i => [FSPerkBonusConstants.ThornsMaxStacks[i], FSPerkBonusConstants.ThornsReflect[i] * 100f])) { IconFile = "juggernaught" },

            new("StaticDischarge", "Static Discharge",
                "Getting hit by a zombie stuns the zombies around you. Specials for half as long.",
                PerkCategory.Blue,
                Levels("{0:0.##} Tile Radius / {1:0.##}s Cooldown",
                    i => [FSPerkBonusConstants.StaticDischargeRadius[i], FSPerkBonusConstants.StaticDischargeCooldown[i]])) { IconFile = "untouchable" },

            new("BuiltToLast", "Built To Last",
                "Your barricades take less damage. You heal when one of them breaks.",
                PerkCategory.Blue,
                Levels("-{0:0.##}% Barricade Damage / +{1:0.##} HP",
                    i => [FSPerkBonusConstants.BuiltToLastPerLevel * (i + 1) * 100f, FSPerkBonusConstants.BuiltToLastHeal[i]])) { IconFile = "cargonian" },

            new("Technician", "Technician",
                "More mine and barricade stock, refilled fully each wave. Max level: +1 of every other.",
                PerkCategory.Green,
                ["+1 Mine/Barricade Stock", "+2 Mine/Barricade Stock", "+3 Mine/Barricade Stock",
                    "+4 Mine/Barricade Stock / +1 Other Deployables"]) { IconFile = "officer" },

            new("Underdog", "Underdog",
                "Gain damage and resistance for every zombie near you, up to 8.",
                PerkCategory.Purple,
                PctTable("+{0}% per Nearby Zombie", FSPerkBonusConstants.UnderdogPerZombie)) { IconFile = "rampage" },

            new("Scavenger", "Scavenger",
                "Kills can drop a supply cache holding ammo, a heal or credits.",
                PerkCategory.Purple,
                PctTable("{0}% Drop Chance", FSPerkBonusConstants.ScavengerChance)) { IconFile = "profiteer" },
        ];

        All = list.ToDictionary(a => a.Id);
    }
}
