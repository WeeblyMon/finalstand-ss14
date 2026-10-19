using Content.Shared.Armor;
using Content.Shared.Clothing;
using Content.Shared.Damage.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Armor.Shop;

public sealed record FSArmorTierDef(string Id, int Price, string SpawnId)
{
    public string Name => Loc.GetString(NameKey);

    public string NameKey => "fs-armor-tier-" + Id.Replace('_', '-');
}

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
        new("security_hardsuit",         20_000, "FSArmorTierSecurityHardsuit"),
        new("ert_security_hardsuit",     30_000, "FSArmorTierERTSecurityHardsuit"),
        new("nukie_commander_hardsuit",  40_000, "FSArmorTierNukieCommanderHardsuit"),
        new("elite_hardsuit",            50_000, "FSArmorTierEliteHardsuit"),
        new("cybersun_juggernaut",       70_000, "FSArmorTierCybersunJuggernaut"),
        new("death_squad_hardsuit",     100_000, "FSArmorTierDeathSquadHardsuit"),
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
