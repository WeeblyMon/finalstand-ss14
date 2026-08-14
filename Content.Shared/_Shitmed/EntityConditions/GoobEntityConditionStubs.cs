// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.EntityConditions;
using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.EntityConditions;

// ── EntityCondition data classes ─────────────────────────────────────────────

/// <summary>Passes if total damage on the entity is within min/max bounds.</summary>
public sealed partial class TotalDamage : EntityConditionBase<TotalDamage>
{
    [DataField] public float Min = 0f;
    [DataField] public float Max = float.MaxValue;
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

/// <summary>Passes if the amount of the current reagent in the bloodstream is within min/max.</summary>
public sealed partial class UniqueBloodstreamChemThreshold : EntityConditionBase<UniqueBloodstreamChemThreshold>
{
    [DataField] public float Min = 0f;
    [DataField] public float Max = float.MaxValue;
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

/// <summary>Passes if the entity's damage in each specified type meets or exceeds the threshold. If Inverse is true, passes when damage is BELOW the threshold.</summary>
public sealed partial class TypedDamageThreshold : EntityConditionBase<TypedDamageThreshold>
{
    [DataField] public DamageSpecifier? Damage;
    [DataField] public bool Inverse = false;
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

/// <summary>Passes if the entity has (or lacks) a component on equipped clothing.</summary>
public sealed partial class HasComponentOnEquipmentCondition : EntityConditionBase<HasComponentOnEquipmentCondition>
{
    [DataField] public ComponentRegistry Components = new();
    [DataField] public bool Invert;
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

/// <summary>Passes if the entity's stamina damage is within min/max bounds.</summary>
public sealed partial class StaminaDamageThreshold : EntityConditionBase<StaminaDamageThreshold>
{
    [DataField] public float Min = 0f;
    [DataField] public float Max = float.MaxValue;
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

// ── Condition systems ─────────────────────────────────────────────────────────

public sealed partial class TotalDamageConditionSystem : EntityConditionSystem<DamageableComponent, TotalDamage>
{
    protected override void Condition(Entity<DamageableComponent> entity, ref EntityConditionEvent<TotalDamage> args)
    {
        var total = (float) entity.Comp.TotalDamage;
        args.Result = total >= args.Condition.Min && total <= args.Condition.Max;
    }
}

/// <summary>
/// Fallback handler — real check is in MetabolizerSystem.CanMetabolizeEffect which has
/// access to the current-reagent quantity context. This stub exists so the condition type
/// is registered; it should never be reached from reagent metabolism.
/// </summary>
public sealed partial class UniqueBloodstreamChemThresholdSystem : EntityConditionSystem<BloodstreamComponent, UniqueBloodstreamChemThreshold>
{
    protected override void Condition(Entity<BloodstreamComponent> entity, ref EntityConditionEvent<UniqueBloodstreamChemThreshold> args)
        => args.Result = true;
}

public sealed partial class TypedDamageThresholdSystem : EntityConditionSystem<DamageableComponent, TypedDamageThreshold>
{
    protected override void Condition(Entity<DamageableComponent> entity, ref EntityConditionEvent<TypedDamageThreshold> args)
    {
        if (args.Condition.Damage == null)
        {
            args.Result = true;
            return;
        }

        foreach (var (type, threshold) in args.Condition.Damage.DamageDict)
        {
            if (!entity.Comp.Damage.DamageDict.TryGetValue(type, out var current) || current < threshold)
            {
                // Threshold not met — pass only if Inverse
                args.Result = args.Condition.Inverse;
                return;
            }
        }

        // All thresholds met — pass unless Inverse
        args.Result = !args.Condition.Inverse;
    }
}

public sealed partial class HasComponentOnEquipmentConditionSystem : EntityConditionSystem<BloodstreamComponent, HasComponentOnEquipmentCondition>
{
    [Dependency] private InventorySystem _inventory = default!;

    protected override void Condition(Entity<BloodstreamComponent> entity, ref EntityConditionEvent<HasComponentOnEquipmentCondition> args)
    {
        var found = false;
        if (_inventory.TryGetContainerSlotEnumerator(entity.Owner, out var enumerator))
        {
            while (enumerator.NextItem(out var item, out _))
            {
                var hasAll = true;
                foreach (var (_, entry) in args.Condition.Components)
                {
                    if (!HasComp(item, entry.Component.GetType()))
                    {
                        hasAll = false;
                        break;
                    }
                }
                if (hasAll)
                {
                    found = true;
                    break;
                }
            }
        }
        args.Result = args.Condition.Invert ? !found : found;
    }
}

public sealed partial class StaminaDamageThresholdSystem : EntityConditionSystem<StaminaComponent, StaminaDamageThreshold>
{
    protected override void Condition(Entity<StaminaComponent> entity, ref EntityConditionEvent<StaminaDamageThreshold> args)
    {
        var stamina = entity.Comp.StaminaDamage;
        args.Result = stamina >= args.Condition.Min && stamina <= args.Condition.Max;
    }
}

/// <summary>
/// Passes if the amount of a specific reagent in the bloodstream is within min/max bounds.
/// In this fork NitrosylPlasmide (the only reagent checked here) is never present, so blood
/// level is always 0 — any max-bounded check (max: 0.1) therefore always passes.
/// </summary>
public sealed partial class BloodReagentThreshold : EntityConditionBase<BloodReagentThreshold>
{
    [DataField] public ProtoId<ReagentPrototype> Reagent = default!;
    [DataField] public float Min = 0f;
    [DataField] public float Max = float.MaxValue;
    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}

public sealed partial class BloodReagentThresholdSystem : EntityConditionSystem<BloodstreamComponent, BloodReagentThreshold>
{
    protected override void Condition(Entity<BloodstreamComponent> entity, ref EntityConditionEvent<BloodReagentThreshold> args)
    {
        // NitrosylPlasmide has no prototype in this fork; blood level is effectively 0.
        // 0 is always <= any max threshold used in the YAML (0.1 / 0.01), so condition passes.
        args.Result = true;
    }
}
