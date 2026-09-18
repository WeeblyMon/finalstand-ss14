
using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Targeting;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Components;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;
using Content.Shared.Body;

namespace Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;

public partial class WoundSystem
{
    [Dependency] private PainSystem _pain = default!;

    private void UpdatePainAfterHealing(EntityUid woundable)
    {
        if (!TryComp<OrganComponent>(woundable, out var bodyPart) || !bodyPart.Body.HasValue)
            return;

        var body = bodyPart.Body.Value;

        if (!TryComp<NerveSystemComponent>(body, out var nerveSystem))
            return;

        if (nerveSystem.Pain > FixedPoint2.Zero)
        {
            var decayDuration = TimeSpan.FromSeconds(nerveSystem.Pain.Float() * 12);

            _pain.StartPainDecay(body, nerveSystem.Pain, decayDuration, nerveSystem);
        }
    }

    #region Public API

    public bool TryHaltAllBleeding(EntityUid woundable, WoundableComponent? component = null, bool force = false)
    {
        if (!Resolve(woundable, ref component)
            || component.Wounds == null
            || component.Wounds.Count == 0)
            return true;

        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            if (force)
            {
                wound.Comp.CanBeHealed = true;
            }

            if (!TryComp<BleedInflicterComponent>(wound, out var bleeds))
                continue;

            bleeds.IsBleeding = false;
        }

        return true;
    }

    public bool TryHealMostSevereBleedingWoundables(EntityUid body, float healAmount, out FixedPoint2 healed, BodyComponent? component = null)
    {
        healed = FixedPoint2.Zero;
        if (!Resolve(body, ref component) || healAmount <= 0)
            return false;

        if (!_lookup.TryGetRootOrgan((body, component), out _))
            return false;

        var bleedingWoundables = new List<(EntityUid Woundable, FixedPoint2 BleedAmount)>();
        foreach (var (bodyPart, _) in _lookup.GetBodyOrgans(body))
        {
            FixedPoint2 totalBleedAmount = FixedPoint2.Zero;
            bool hasBleedingWounds = false;
            foreach (var wound in GetWoundableWounds(bodyPart))
            {
                if (!TryComp<BleedInflicterComponent>(wound, out var bleeds) || !bleeds.IsBleeding)
                    continue;

                hasBleedingWounds = true;
                totalBleedAmount += bleeds.BleedingAmount;
            }

            if (hasBleedingWounds)
                bleedingWoundables.Add((bodyPart, totalBleedAmount));
        }

        var sortedWoundables = bleedingWoundables
            .OrderByDescending(x => x.BleedAmount)
            .Select(x => x.Woundable)
            .ToList();

        float remainingHealAmount = healAmount * sortedWoundables.Count;
        bool anyHealed = false;

        foreach (var woundable in sortedWoundables)
        {
            if (remainingHealAmount <= 0)
                break;

            if (TryHealBleedingWounds(woundable, -remainingHealAmount, out var modifiedBleed))
            {
                anyHealed = true;
                healed += modifiedBleed;
                remainingHealAmount -= modifiedBleed.Float();

                if (remainingHealAmount <= 0)
                    break;
            }
        }

        return anyHealed;
    }

    public bool TryHealBleedingWounds(EntityUid woundable, float bleedStopAbility, out FixedPoint2 modifiedBleed, WoundableComponent? component = null)
    {
        modifiedBleed = FixedPoint2.Zero; // Goobstation
        if (!Resolve(woundable, ref component))
            return false;

        foreach (var wound in GetWoundableWounds(woundable, component))
        {
            if (!TryComp<BleedInflicterComponent>(wound, out var bleeds)
                || !bleeds.IsBleeding)
                continue;

            if (-bleedStopAbility > bleeds.BleedingAmount) // Goobstation
            {
                modifiedBleed = bleeds.BleedingAmount; // Goobstation
                bleeds.BleedingAmountRaw = 0;
                bleeds.IsBleeding = false;
                bleeds.Scaling = 0;
            }
            else
            {
                bleeds.BleedingAmountRaw += bleedStopAbility; // Goobstation
                modifiedBleed = -bleedStopAbility; // Goobstation
            }

            Dirty(wound, bleeds);
        }
        return modifiedBleed <= -bleedStopAbility; // Goobstation
    }

    public void ForceHealWoundsOnWoundable(EntityUid woundable,
        out FixedPoint2 healed,
        DamageGroupPrototype? damageGroup = null,
        WoundableComponent? component = null)
    {
        healed = 0;
        if (!Resolve(woundable, ref component))
            return;

        var woundsToHeal =
            GetWoundableWounds(woundable, component)
                .Where(wound => damageGroup == null || wound.Comp.DamageGroup == damageGroup)
                .ToList();

        foreach (var wound in woundsToHeal)
        {
            healed += wound.Comp.WoundSeverityPoint;
            RemoveWound(wound, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);

        if (woundsToHeal.Count > 0)
        {
            UpdatePainAfterHealing(woundable);
        }
    }

    public bool TryHealWoundsOnWoundable(EntityUid woundable,
        FixedPoint2 healAmount,
        out FixedPoint2 healed,
        WoundableComponent? component = null,
        DamageGroupPrototype? damageGroup = null,
        bool ignoreMultipliers = false,
        bool ignoreBlockers = false)
    {
        healed = 0;
        if (!Resolve(woundable, ref component)
            || component.Wounds == null)
            return false;

        var woundsToHeal =
            (from wound in component.Wounds.ContainedEntities
                let woundComp = Comp<WoundComponent>(wound)
                where CanHealWound(wound, woundComp, ignoreBlockers)
                where damageGroup == null || damageGroup == woundComp.DamageGroup
                select (wound, woundComp)).Select(dummy => (Entity<WoundComponent>) dummy)
            .ToList();

        if (woundsToHeal.Count == 0)
            return false;

        var healNumba = healAmount / woundsToHeal.Count;
        var actualHeal = FixedPoint2.Zero;
        foreach (var wound in woundsToHeal)
        {
            var heal = ignoreMultipliers
                ? ApplyHealingRateMultipliers(wound, woundable, -healNumba, component)
                : -healNumba;

            actualHeal += -heal;
            ApplyWoundSeverity(wound, heal, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);

        healed = actualHeal;
        return actualHeal > 0;
    }

    public bool TryHealWoundsOnWoundable(EntityUid woundable,
        FixedPoint2 healAmount,
        string damageType,
        out FixedPoint2 healed,
        WoundableComponent? component = null,
        bool ignoreMultipliers = false,
        bool ignoreBlockers = false)
    {
        healed = 0;
        if (!Resolve(woundable, ref component, false)
            || component.Wounds == null)
            return false;

        var woundsToHeal =
            (from wound in component.Wounds.ContainedEntities
                let woundComp = Comp<WoundComponent>(wound)
                where CanHealWound(wound, woundComp, ignoreBlockers)
                where damageType == woundComp.DamageType
                select (wound, woundComp)).Select(dummy => (Entity<WoundComponent>) dummy)
            .ToList();

        if (woundsToHeal.Count == 0)
            return false;

        var healNumba = healAmount / woundsToHeal.Count;
        var actualHeal = FixedPoint2.Zero;
        foreach (var wound in woundsToHeal)
        {
            var heal = ignoreMultipliers
                ? ApplyHealingRateMultipliers(wound, woundable, -healNumba, component)
                : -healNumba;

            actualHeal += -heal;
            ApplyWoundSeverity(wound, heal, wound);
        }

        UpdateWoundableIntegrity(woundable, component);
        CheckWoundableSeverityThresholds(woundable, component);

        healed = actualHeal;
        return actualHeal > 0;
    }

    public bool TryHealWoundsOnWoundable(EntityUid woundable,
        DamageSpecifier damage,
        out Dictionary<string, FixedPoint2> healed,
        WoundableComponent? component = null,
        bool ignoreMultipliers = false)
    {
        healed = [];
        if (!Resolve(woundable, ref component, false))
            return false;

        foreach (var (key, value) in damage.DamageDict)
        {
            if (TryHealWoundsOnWoundable(woundable, -value, key, out var tempHealed, component, ignoreMultipliers))
            {
                healed.Add(key, tempHealed);
                continue;
            }
        }

        return healed.Any();
    }

    public bool TryGetWoundableWithMostDamage(
        EntityUid body,
        [NotNullWhen(true)] out Entity<WoundableComponent>? woundable,
        string? damageGroup = null,
        bool healable = false)
    {
        var biggestDamage = FixedPoint2.Zero;

        woundable = null;
        foreach (var bodyPart in _lookup.GetBodyOrgans(body))
        {
            if (!TryComp<WoundableComponent>(bodyPart.Owner, out var woundableComp))
                continue;

            var woundableDamage = GetWoundableSeverityPoint(bodyPart.Owner, woundableComp, damageGroup, healable);
            if (woundableDamage <= biggestDamage)
                continue;

            biggestDamage = woundableDamage;
            woundable = (bodyPart.Owner, woundableComp);
        }

        return woundable != null;
    }

    public bool HasDamageOfType(
        EntityUid woundable,
        string damageType,
        bool healable = false)
    {
        if (healable)
            return GetWoundableWounds(woundable)
                .Any(wound => wound.Comp.DamageType == damageType);

        return GetWoundableWounds(woundable).Any(wound => wound.Comp.DamageType == damageType);
    }

    public bool HasDamageOfGroup(
        EntityUid woundable,
        string damageGroup,
        bool healable = false)
    {
        if (healable)
            return GetWoundableWounds(woundable)
                .Any(wound => wound.Comp.DamageGroup == damageGroup);

        return GetWoundableWounds(woundable).Any(wound => wound.Comp.DamageGroup == damageGroup);
    }

    public FixedPoint2 ApplyHealingRateMultipliers(EntityUid wound,
        EntityUid woundable,
        FixedPoint2 severity,
        WoundableComponent? component = null,
        WoundComponent? woundComp = null)
    {
        if (!Resolve(woundable, ref component))
            return severity;

        if (!Resolve(wound, ref woundComp, false)
            || !woundComp.CanBeHealed)
            return FixedPoint2.Zero;

        var woundHealingMultiplier =
            _prototype.Index<DamageTypePrototype>(Comp<WoundComponent>(wound).DamageType).WoundHealingMultiplier;

        if (component.HealingMultipliers.Count == 0)
            return severity * woundHealingMultiplier;

        var toMultiply =
            component.HealingMultipliers.Sum(multiplier => (float) multiplier.Value.Change) / component.HealingMultipliers.Count;
        return severity * toMultiply * woundHealingMultiplier;
    }

    public bool TryAddHealingRateMultiplier(EntityUid owner, EntityUid woundable, string identifier, FixedPoint2 change, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component) || !_net.IsServer)
            return false;

        return component.HealingMultipliers.TryAdd(owner, new WoundableHealingMultiplier(change, identifier));
    }

    public bool TryRemoveHealingRateMultiplier(EntityUid owner, EntityUid woundable, WoundableComponent? component = null)
    {
        if (!Resolve(woundable, ref component)  || !_net.IsServer)
            return false;

        return component.HealingMultipliers.Remove(owner);
    }

    public bool CanHealWound(EntityUid wound, WoundComponent? comp = null, bool ignoreBlockers = false)
    {
        if (!Resolve(wound, ref comp))
            return false;

        if (!ignoreBlockers && !comp.CanBeHealed)
            return false;

        var holdingWoundable = comp.HoldingWoundable;

        var ev = new WoundHealAttemptOnWoundableEvent((wound, comp));
        RaiseLocalEvent(holdingWoundable, ref ev);

        if (ev.Cancelled)
            return false;

        var ev1 = new WoundHealAttemptEvent((holdingWoundable, Comp<WoundableComponent>(holdingWoundable)), ignoreBlockers);
        RaiseLocalEvent(wound, ref ev1);

        return !ev1.Cancelled;
    }

    public bool TryGetAllOwnerWounds(EntityUid target, [NotNullWhen(true)] out List<Entity<WoundComponent>> wounds)
    {
        wounds = [];

        if (!_lookup.TryGetRootOrgan(target, out var bodyRoot))
            return false;

        wounds = GetAllWounds(bodyRoot.Owner).ToList();

        return wounds.Any();
    }

    public bool TryGetAllOwnerWoundedParts(EntityUid target, [NotNullWhen(true)] out List<Entity<WoundableComponent>> woundables)
    {
        woundables = [];

        foreach (var bodyPart in _lookup.GetBodyOrgans(target))
        {
            if (!TryComp<WoundableComponent>(bodyPart.Owner, out var woundableComp) || !woundableComp.Wounds.ContainedEntities.Any())
                continue;

            woundables.Add((bodyPart.Owner, woundableComp));
        }

        return woundables.Any();
    }

    public bool TryHealWoundsOnOwner(EntityUid target, DamageSpecifier healing, bool ignoreBlockers = false)
    {
        var healedWounds = 0;

        if (!TryGetAllOwnerWoundedParts(target, out var woundables) || !TryGetAllOwnerWounds(target, out var wounds))
            return false;

        DamageSpecifier healingPerPart = new DamageSpecifier(healing);
        healingPerPart.DamageDict.Clear();

        var woundCountByType = wounds
            .GroupBy(w => w.Comp.DamageType)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var healingType in healing.DamageDict)
        {
            var splitAmount = woundCountByType.GetValueOrDefault(healingType.Key, 0);

            var splittedDamage = splitAmount != 0 ? healingType.Value / splitAmount : healingType.Value;

            healingPerPart.DamageDict.Add(healingType.Key, splittedDamage);
        }

        foreach (var woundable in woundables)
        {
            if (!TryHealWoundsOnWoundable(woundable.Owner, healingPerPart, out var healed, woundable.Comp, ignoreBlockers))
                continue;

            healedWounds++;
        }

        return healedWounds > 0;
    }

    #endregion

    public bool TryHealBleedsOnBody(EntityUid body, float bleedStopAbility, TargetBodyPart? targeted = null)
    {
        var healedAny = false;

        foreach (var organ in _lookup.GetBodyOrgans(body))
        {
            if (!TryComp<WoundableComponent>(organ.Owner, out var woundable))
                continue;

            if (targeted != null && _lookup.GetTarget(organ.Owner) is { } part && part != targeted)
                continue;

            if (TryHealBleedingWounds(organ.Owner, bleedStopAbility, out _, woundable))
                healedAny = true;
        }

        return healedAny;
    }

    public bool IsAnyWoundableBleeding(EntityUid body, TargetBodyPart? targeted = null)
    {
        foreach (var organ in _lookup.GetBodyOrgans(body))
        {
            if (!TryComp<WoundableComponent>(organ.Owner, out var woundable))
                continue;

            if (targeted != null && _lookup.GetTarget(organ.Owner) is { } part && part != targeted)
                continue;

            foreach (var wound in GetWoundableWounds(organ.Owner, woundable))
            {
                if (TryComp<BleedInflicterComponent>(wound, out var bleeds) && bleeds.IsBleeding)
                    return true;
            }
        }

        return false;
    }
}
