using Content.Shared.Armor;
using Content.Shared.Clothing;
using Content.Shared.Damage.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Armor.Shop;

public sealed record FSArmorTierDef(string Id, string Name, int Price, string SpawnId);

// Read from the hardsuit prototype, so the shop cannot advertise numbers the item lacks.
public sealed record FSArmorTierStats(
    IReadOnlyDictionary<string, float> Resistances,
    float SpeedMod,
    float StaminaReduction);

public static class FSArmorShopDefs
{
    public const float RefundFraction = 0.5f;

    public static readonly IReadOnlyList<FSArmorTierDef> Tiers = new FSArmorTierDef[]
    {
        new("security_hardsuit",        "Security Hardsuit",             20_000, "FSArmorTierSecurityHardsuit"),
        new("ert_security_hardsuit",    "ERT Security Hardsuit",         30_000, "FSArmorTierERTSecurityHardsuit"),
        new("nukie_commander_hardsuit", "Nukie Commander Hardsuit",      40_000, "FSArmorTierNukieCommanderHardsuit"),
        new("elite_hardsuit",           "Elite Hardsuit",                50_000, "FSArmorTierEliteHardsuit"),
        new("cybersun_juggernaut",      "Cybersun Juggernaut Hardsuit",  70_000, "FSArmorTierCybersunJuggernaut"),
        new("death_squad_hardsuit",     "Death Squad Hardsuit",         100_000, "FSArmorTierDeathSquadHardsuit"),
    };

    public static FSArmorTierDef? GetTier(string? id)
    {
        if (id == null)
            return null;

        foreach (var tier in Tiers)
        {
            if (tier.Id == id)
                return tier;
        }

        return null;
    }

    public static int GetRefund(string? currentTierId)
    {
        var current = GetTier(currentTierId);
        return current == null ? 0 : (int) (current.Price * RefundFraction);
    }

    public static int GetNetCost(string? currentTierId, FSArmorTierDef target)
    {
        return target.Price - GetRefund(currentTierId);
    }

    public static FSArmorTierStats GetStats(FSArmorTierDef tier, IPrototypeManager protos, IComponentFactory factory)
    {
        var resistances = new Dictionary<string, float>();
        var speedMod = 0f;
        var staminaReduction = 0f;

        if (!protos.TryIndex<EntityPrototype>(tier.SpawnId, out var proto))
            return new FSArmorTierStats(resistances, speedMod, staminaReduction);

        if (proto.TryGetComponent<ArmorComponent>(out var armor, factory))
        {
            foreach (var (damageType, coefficient) in armor.Modifiers.Coefficients)
                resistances[damageType] = 1f - coefficient;
        }

        if (proto.TryGetComponent<ClothingSpeedModifierComponent>(out var speed, factory))
            speedMod = speed.WalkModifier - 1f;

        if (proto.TryGetComponent<StaminaResistanceComponent>(out var stamina, factory))
            staminaReduction = 1f - stamina.DamageCoefficient;

        return new FSArmorTierStats(resistances, speedMod, staminaReduction);
    }
}
