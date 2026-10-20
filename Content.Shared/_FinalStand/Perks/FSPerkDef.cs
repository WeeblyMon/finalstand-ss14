using System.Globalization;
using System.Linq;

namespace Content.Shared._FinalStand.Perks;

public enum PerkCategory { Red, Blue, Green, Yellow, Purple }

// Text lives in perks.ftl under fs-perk-<id>-*; the numbers in each level line come from FSPerkBonusConstants,
// so the shop can never advertise a rate the maths does not pay.
public sealed class FSPerkDef
{
    public string Id { get; }
    public PerkCategory Category { get; }

    public string? IconFile { get; init; }  // filename without extension; defaults to Id.ToLowerInvariant()

    public const int MaxLevel = 4;
    public const int SlotCount = 6;

    public static int CostForUpgrade(int currentLevel) => currentLevel + 1;

    public readonly string NameKey;
    public readonly string DescriptionKey;

    private readonly string _levelKey;
    private readonly string[] _fixedLevelKeys;

    // Arguments for each level's line; null where the line is fixed text with a key of its own.
    private readonly (string, object)[]?[] _levelArgs;

    private FSPerkDef(string id, PerkCategory category, (string, object)[]?[] levelArgs)
    {
        if (levelArgs.Length != MaxLevel)
            throw new ArgumentException($"FSPerkDef '{id}': expected {MaxLevel} levels, got {levelArgs.Length}.");

        Id = id;
        Category = category;
        _levelArgs = levelArgs;

        var key = "fs-perk-" + Kebab(id);
        NameKey = key + "-name";
        DescriptionKey = key + "-desc";
        _levelKey = key + "-level";
        _fixedLevelKeys = Enumerable.Range(1, MaxLevel).Select(level => $"{key}-level-{level}").ToArray();
    }

    public string Name => Loc.GetString(NameKey);

    public string Description => Loc.GetString(DescriptionKey);

    // Index 0 is level 1.
    public string LevelEffect(int index)
    {
        return _levelArgs[index] is { } args
            ? Loc.GetString(_levelKey, args)
            : Loc.GetString(_fixedLevelKeys[index]);
    }

    public IEnumerable<string> LevelKeys()
    {
        for (var i = 0; i < MaxLevel; i++)
            yield return _levelArgs[i] != null ? _levelKey : _fixedLevelKeys[i];
    }

    private static string Kebab(string id)
    {
        var chars = new List<char>(id.Length + 4);
        for (var i = 0; i < id.Length; i++)
        {
            if (i > 0 && char.IsUpper(id[i]))
                chars.Add('-');

            chars.Add(char.ToLowerInvariant(id[i]));
        }

        return new string(chars.ToArray());
    }

    private static readonly string[] ArgNames = ["value", "value2"];

    private static (string, object)[] Args(params object[] values)
    {
        var args = new (string, object)[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            // Not Convert: the client sandbox doesn't allow it.
            args[i] = (ArgNames[i], values[i] switch
            {
                float f => ((double) f).ToString("0.##", CultureInfo.InvariantCulture),
                double d => d.ToString("0.##", CultureInfo.InvariantCulture),
                int n => n.ToString(CultureInfo.InvariantCulture),
                var other => other.ToString() ?? string.Empty,
            });
        }

        return args;
    }

    // Pct scales by 100, Flat does not; both multiply the per-level rate by the level.
    private static (string, object)[]?[] Pct(params float[] rates) => Rows(100f, rates);

    private static (string, object)[]?[] Flat(params float[] amounts) => Rows(1f, amounts);

    // For perks that don't scale linearly, so the table itself is the source of truth.
    private static (string, object)[]?[] Table(float[] perLevel)
    {
        if (perLevel.Length != MaxLevel)
            throw new ArgumentException($"Perk table needs {MaxLevel} entries, got {perLevel.Length}.");

        return perLevel.Select(v => ((string, object)[]?) Args(v)).ToArray();
    }

    private static (string, object)[]?[] PctTable(float[] perLevel) => Table(perLevel.Select(v => v * 100f).ToArray());

    private static (string, object)[]?[] Levels(Func<int, object[]> args)
        => Enumerable.Range(0, MaxLevel).Select(i => ((string, object)[]?) Args(args(i))).ToArray();

    private static (string, object)[]?[] Fixed() => new (string, object)[]?[MaxLevel];

    private static (string, object)[]?[] Rows(float scale, float[] perLevel)
    {
        var rows = new (string, object)[]?[MaxLevel];
        for (var level = 1; level <= MaxLevel; level++)
        {
            var values = new object[perLevel.Length];
            for (var i = 0; i < perLevel.Length; i++)
                values[i] = perLevel[i] * level * scale;

            rows[level - 1] = Args(values);
        }

        return rows;
    }

    public static readonly IReadOnlyDictionary<string, FSPerkDef> All;

    static FSPerkDef()
    {
        FSPerkDef[] list =
        [
            new("StoppingPower", PerkCategory.Red, Pct(FSPerkBonusConstants.StoppingPowerPerLevel)),

            new("BulletStorm", PerkCategory.Red, Pct(FSPerkBonusConstants.BulletStormPerLevel)),

            new("Juggernaught", PerkCategory.Blue, Pct(FSPerkBonusConstants.JuggernaughtPerLevel)),

            new("Lightweight", PerkCategory.Green, Pct(FSPerkBonusConstants.LightweightPerLevel)),

            new("Profiteer", PerkCategory.Yellow, Pct(FSPerkBonusConstants.ProfiteerFraction)),

            new("SwordAndShield", PerkCategory.Purple,
                Pct(FSPerkBonusConstants.SwordAndShieldPerLevel, FSPerkBonusConstants.SwordAndShieldResistPerLevel)),

            new("DeathAura", PerkCategory.Red, Flat(FSPerkBonusConstants.DeathAuraStacksPerLevel)),

            new("Adrenaline", PerkCategory.Green, Table(FSPerkBonusConstants.AdrenalineSeconds)),

            new("SpeedDemon", PerkCategory.Green, Pct(FSPerkBonusConstants.SpeedDemonPerLevel)),

            new("Rampage", PerkCategory.Purple, Fixed()),

            new("Investor", PerkCategory.Yellow, Pct(FSPerkBonusConstants.InvestorPerLevel)),

            new("MutualFund", PerkCategory.Yellow, Pct(FSPerkBonusConstants.MutualFundPerLevel)),

            new("LifeLeech", PerkCategory.Blue, Table(FSPerkBonusConstants.LifeLeechHeal)),

            new("Untouchable", PerkCategory.Blue, Fixed()),

            new("Martyr", PerkCategory.Red, Fixed()),

            new("GlassCannon", PerkCategory.Red, Pct(FSPerkBonusConstants.GlassCannonPerLevel)),

            new("Pacifist", PerkCategory.Blue, Pct(FSPerkBonusConstants.PacifistResistPerLevel)),

            new("FieldMedic", PerkCategory.Blue, Pct(FSPerkBonusConstants.FieldMedicPerLevel)),

            new("Cargonian", PerkCategory.Green, Fixed()),

            new("LegBreaker", PerkCategory.Blue, Flat(FSPerkBonusConstants.LegBreakerStaminaPerLevel)),

            new("BackBreaker", PerkCategory.Green, Fixed()),

            new("KnockbackBlast", PerkCategory.Blue, Fixed()),

            new("DeepImpact", PerkCategory.Red, Fixed()),

            new("Officer", PerkCategory.Green, Pct(FSPerkBonusConstants.OfficerBuffPerLevel)),

            new("HarvesterTuning", PerkCategory.Yellow, Fixed()),

            new("CombatMedic", PerkCategory.Blue, Pct(FSPerkBonusConstants.CombatMedicPerLevel)),

            new("Ravage", PerkCategory.Red,
                Pct(FSPerkBonusConstants.RavageCritPerLevel, FSPerkBonusConstants.RavageAttackSpeedPerLevel)),

            new("CriticalPlus", PerkCategory.Red, Pct(FSPerkBonusConstants.CriticalPlusPerLevel)),

            new("Implosion", PerkCategory.Red, PctTable(FSPerkBonusConstants.ImplosionDamage)),

            new("Rifleman", PerkCategory.Red, Pct(FSPerkBonusConstants.RiflemanPerLevel)),

            new("Berserker", PerkCategory.Red, Pct(FSPerkBonusConstants.BerserkerPerLevel)),

            new("Undying", PerkCategory.Blue, Table(FSPerkBonusConstants.UndyingSeconds)),

            new("ManOnFire", PerkCategory.Blue, PctTable(FSPerkBonusConstants.ManOnFireResist)),

            new("Bandolier", PerkCategory.Green, Pct(FSPerkBonusConstants.BandolierPerLevel)),

            new("ShadowRounds", PerkCategory.Red, PctTable(FSPerkBonusConstants.ShadowRoundsChance)),

            new("Bloodload", PerkCategory.Green, Pct(FSPerkBonusConstants.BloodloadPerLevel)),

            new("Speedload", PerkCategory.Red, Pct(FSPerkBonusConstants.SpeedloadPerLevel)),

            new("Executioner", PerkCategory.Red, Pct(FSPerkBonusConstants.ExecutionerPerLevel)),

            new("Shredder", PerkCategory.Red, PctTable(FSPerkBonusConstants.ShredderPerStack)),

            new("SpecialisedKilling", PerkCategory.Red, Pct(FSPerkBonusConstants.SpecialisedKillingPerLevel)),

            new("Thorns", PerkCategory.Blue,
                Levels(i => [FSPerkBonusConstants.ThornsMaxStacks[i], FSPerkBonusConstants.ThornsReflect[i] * 100f])),

            new("StaticDischarge", PerkCategory.Blue,
                Levels(i => [FSPerkBonusConstants.StaticDischargeRadius[i], FSPerkBonusConstants.StaticDischargeCooldown[i]])),

            new("BuiltToLast", PerkCategory.Blue,
                Levels(i => [FSPerkBonusConstants.BuiltToLastPerLevel * (i + 1) * 100f, FSPerkBonusConstants.BuiltToLastHeal[i]])),

            new("Technician", PerkCategory.Green, Fixed()),

            new("Underdog", PerkCategory.Purple, PctTable(FSPerkBonusConstants.UnderdogPerZombie)),

            new("Scavenger", PerkCategory.Purple, PctTable(FSPerkBonusConstants.ScavengerChance)),
        ];

        All = list.ToDictionary(a => a.Id);
    }
}
