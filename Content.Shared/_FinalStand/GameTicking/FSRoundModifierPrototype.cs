using System.Linq;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.GameTicking;

[Serializable, NetSerializable]
public enum FSModifierTier : byte
{
    Environmental,
    Mutation,
    RulesOfEngagement,
}

[Prototype("fsRoundModifier")]
public sealed partial class FSRoundModifierPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name { get; private set; }

    [DataField(required: true)]
    public LocId Description { get; private set; }

    [DataField]
    public FSModifierTier Tier { get; private set; } = FSModifierTier.Environmental;

    [DataField]
    public float RewardBonus { get; private set; }

    [DataField]
    public float Weight { get; private set; } = 1f;

    [DataField]
    public float EnemyHealth { get; private set; } = 1f;

    [DataField]
    public float EnemyCount { get; private set; } = 1f;

    [DataField]
    public float EnemyDamage { get; private set; } = 1f;

    [DataField]
    public float EnemySpeed { get; private set; } = 1f;

    [DataField]
    public float EnemyMoney { get; private set; } = 1f;

    [DataField]
    public float PrepDuration { get; private set; } = 1f;

    [DataField]
    public float SpecialChance { get; private set; } = 1f;

    [DataField]
    public ComponentRegistry EnemyComponents { get; private set; } = new();

    [DataField]
    public List<ProtoId<FSRoundModifierPrototype>> Incompatible { get; private set; } = new();
}

public static class FSRoundModifierRules
{
    public const float MaxRewardBonus = 1f;

    public static float RewardMultiplier(IEnumerable<FSRoundModifierPrototype> modifiers)
        => 1f + Math.Clamp(modifiers.Sum(m => m.RewardBonus), 0f, MaxRewardBonus);

    public static float Product(IEnumerable<FSRoundModifierPrototype> modifiers, Func<FSRoundModifierPrototype, float> factor)
        => modifiers.Aggregate(1f, (acc, m) => acc * factor(m));

    public static bool Compatible(FSRoundModifierPrototype a, FSRoundModifierPrototype b)
        => a.ID != b.ID && !a.Incompatible.Contains(b.ID) && !b.Incompatible.Contains(a.ID);

    public static List<FSRoundModifierPrototype> Roll(IReadOnlyList<FSRoundModifierPrototype> pool, int count, Func<float> nextFloat)
    {
        var chosen = new List<FSRoundModifierPrototype>();
        var candidates = pool.Where(m => m.Weight > 0f).ToList();

        while (chosen.Count < count)
        {
            candidates.RemoveAll(c => chosen.Any(p => !Compatible(p, c)));
            if (candidates.Count == 0)
                break;

            var total = candidates.Sum(c => c.Weight);
            var pick = nextFloat() * total;
            var picked = candidates[^1];
            foreach (var candidate in candidates)
            {
                pick -= candidate.Weight;
                if (pick > 0f)
                    continue;
                picked = candidate;
                break;
            }

            chosen.Add(picked);
        }

        return chosen;
    }
}
