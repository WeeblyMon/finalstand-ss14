using Content.Server._FinalStand.Crit;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Item;
using Content.Shared.Mind;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Perks;

// Player-side crit bonuses (Ravage, Critical Plus, Rifleman), read by every crit roll.
public sealed partial class FSPerkCritSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSWeaponClassifierSystem _classifier = default!;
    [Dependency] private CritSystem _crit = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedItemSystem _item = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPlayerMeleeHitEvent>(OnPlayerMeleeHit);
    }

    public float GetCritChanceBonus(EntityUid user, EntityUid weapon, bool melee, List<(string PerkId, float Chance)>? sources = null)
    {
        if (!TryGetPerks(user, out var perks))
            return 0f;

        var bonus = 0f;

        void Add(string perkId, float chance)
        {
            if (chance <= 0f)
                return;
            bonus += chance;
            sources?.Add((perkId, chance));
        }

        if (melee)
        {
            Add("Ravage", perks.GetSlottedLevel("Ravage") * FSPerkBonusConstants.RavageCritPerLevel);
            return bonus;
        }

        if (_classifier.IsSemiAutoRifle(weapon))
            Add("Rifleman", perks.GetSlottedLevel("Rifleman") * FSPerkBonusConstants.RiflemanPerLevel);

        Add("ConcealedCarry", perks.GetSlottedLevel("ConcealedCarry") * FSPerkBonusConstants.ConcealedCarryPerLevel
            * ConcealedCarryFactor(weapon));

        return bonus;
    }

    public float ConcealedCarryFactor(EntityUid weapon)
    {
        if (!TryComp<ItemComponent>(weapon, out var item))
            return 0f;

        var weight = _item.GetSizePrototype(item.Size).Weight;
        return weight >= HugeWeight ? 0f : weight >= LargeWeight ? 0.5f : 1f;
    }

    public float GetCritMultiplierBonus(EntityUid user, EntityUid weapon)
    {
        if (!TryGetPerks(user, out var perks))
            return 0f;

        var bonus = perks.GetSlottedLevel("CriticalPlus") * FSPerkBonusConstants.CriticalPlusPerLevel;
        if (_classifier.IsSemiAutoRifle(weapon))
            bonus += perks.GetSlottedLevel("Rifleman") * FSPerkBonusConstants.RiflemanPerLevel;

        return bonus;
    }

    private const int LargeWeight = 8;
    private const int HugeWeight = 16;

    public static float CombineChance(float a, float b) => MathF.Min(1f - (1f - a) * (1f - b), 1f);

    // Upgraded melee weapons with their own crit chance roll in FSMeleeUpgradeRuntimeSystem instead.
    private void OnPlayerMeleeHit(FSPlayerMeleeHitEvent ev)
    {
        var hit = ev.Hit;
        if (TryComp<FSWeaponUpgradeStateComponent>(hit.Weapon, out var state) && state.CritChance > 0f)
            return;

        var chance = GetCritChanceBonus(hit.User, hit.Weapon, melee: true);
        if (chance <= 0f || !_random.Prob(MathF.Min(chance, 1f)))
            return;

        var multiplier = FSPerkBonusConstants.PerkMeleeCritMultiplier + GetCritMultiplierBonus(hit.User, hit.Weapon);
        hit.BonusDamage += hit.BaseDamage * (multiplier - 1f);

        foreach (var target in hit.HitEntities)
            _crit.MarkPendingCrit(hit.User, target);
    }

    private bool TryGetPerks(EntityUid user, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out FSPerkLevelsComponent? perks)
    {
        perks = null;
        return _mind.TryGetMind(user, out var mindId, out _) && TryComp(mindId, out perks);
    }
}
