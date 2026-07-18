namespace Content.Shared._FinalStand.Armor.Shop;

public sealed record FSArmorTierDef(
    string Id,
    string Name,
    int Price,
    float Blunt,
    float Slash,
    float Piercing,
    float Heat,
    float Radiation,
    float Caustic,
    float Shock,
    float Explosion,
    float SpeedMod,
    float StaminaReduction,
    string SpawnId);

public static class FSArmorShopDefs
{
    public static readonly IReadOnlyList<FSArmorTierDef> Tiers = new FSArmorTierDef[]
    {
        //                                                                         Blunt  Slash  Pierce Heat   Rad    Caustic Shock  Expl   Speed  Stam
        new("security_hardsuit",        "Security Hardsuit",            20_000, 0.40f, 0.40f, 0.40f, 0.20f, 0.00f, 0.30f, 0.20f, 0.60f, -0.25f, 0.00f, "FSArmorTierSecurityHardsuit"),
        new("ert_security_hardsuit",    "ERT Security Hardsuit",        30_000, 0.50f, 0.50f, 0.50f, 0.50f, 0.50f, 0.50f, 0.30f, 0.55f, -0.15f, 0.40f, "FSArmorTierERTSecurityHardsuit"),
        new("nukie_commander_hardsuit", "Nukie Commander Hardsuit",     40_000, 0.60f, 0.60f, 0.60f, 0.60f, 0.65f, 0.60f, 0.40f, 0.65f, -0.10f, 0.50f, "FSArmorTierNukieCommanderHardsuit"),
        new("elite_hardsuit",           "Elite Hardsuit",               50_000, 0.70f, 0.70f, 0.70f, 0.70f, 0.75f, 0.70f, 0.50f, 0.75f, -0.05f, 0.50f, "FSArmorTierEliteHardsuit"),
        new("cybersun_juggernaut",      "Cybersun Juggernaut Hardsuit", 70_000, 0.75f, 0.75f, 0.75f, 0.85f, 0.75f, 0.75f, 0.60f, 0.85f,  0.00f, 0.75f, "FSArmorTierCybersunJuggernaut"),
        new("death_squad_hardsuit",     "Death Squad Hardsuit",        100_000, 0.85f, 0.85f, 0.85f, 0.90f, 0.85f, 0.85f, 0.70f, 0.90f,  0.15f, 0.85f, "FSArmorTierDeathSquadHardsuit"),
    };

    public static FSArmorTierDef? GetTier(string id)
    {
        foreach (var tier in Tiers)
            if (tier.Id == id) return tier;
        return null;
    }
}
