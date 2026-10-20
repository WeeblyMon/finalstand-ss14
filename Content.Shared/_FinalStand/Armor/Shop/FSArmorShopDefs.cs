using Content.Shared.Armor;
using Content.Shared.Clothing;
using Content.Shared.Damage.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Armor.Shop;

// Read from the hardsuit prototype, so the shop cannot advertise numbers the item lacks.
public sealed record FSArmorTierStats(
    IReadOnlyDictionary<string, float> Resistances,
    float SpeedMod,
    float StaminaReduction);

public static class FSArmorShopDefs
{
    public const float RefundFraction = 0.5f;

    // Cheapest first, the order the shop lists them in.
    public static List<FSArmorTierPrototype> Tiers(IPrototypeManager protos)
    {
        var tiers = new List<FSArmorTierPrototype>(protos.EnumeratePrototypes<FSArmorTierPrototype>());
        tiers.Sort((a, b) => a.Price != b.Price ? a.Price.CompareTo(b.Price) : string.CompareOrdinal(a.ID, b.ID));
        return tiers;
    }

    public static int GetRefund(FSArmorTierPrototype? current)
    {
        return current == null ? 0 : (int) (current.Price * RefundFraction);
    }

    public static int GetNetCost(FSArmorTierPrototype? current, FSArmorTierPrototype target)
    {
        return target.Price - GetRefund(current);
    }

    public static FSArmorTierStats GetStats(FSArmorTierPrototype tier, IPrototypeManager protos, IComponentFactory factory)
    {
        var resistances = new Dictionary<string, float>();
        var speedMod = 0f;
        var staminaReduction = 0f;

        if (!protos.TryIndex(tier.Spawn, out var proto))
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
