using Content.Server._FinalStand.Crit;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;
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

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPlayerMeleeHitEvent>(OnPlayerMeleeHit);
    }

    public float GetCritChanceBonus(EntityUid user, EntityUid weapon, bool melee)
    {
        if (!TryGetPerks(user, out var perks))
            return 0f;

        var bonus = 0f;
        if (melee)
            bonus += perks.GetSlottedLevel("Ravage") * FSPerkBonusConstants.RavageCritPerLevel;
        else if (_classifier.IsSemiAutoRifle(weapon))
            bonus += perks.GetSlottedLevel("Rifleman") * FSPerkBonusConstants.RiflemanPerLevel;

        return bonus;
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
