// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// ReagentThreshold is handled as a special case in MetabolizerSystem.CanMetabolizeEffect,
// which has access to the current-reagent quantity context. This event handler exists only
// so the condition type is registered; it should never be reached from reagent metabolism.

using Content.Shared.Body.Components;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.EntityConditions;

public sealed partial class ReagentThresholdConditionSystem : EntityConditionSystem<BloodstreamComponent, ReagentThreshold>
{
    protected override void Condition(Entity<BloodstreamComponent> entity, ref EntityConditionEvent<ReagentThreshold> args)
    {
        // Fallback: always passes. Real check is in MetabolizerSystem.CanMetabolizeEffect.
        args.Result = true;
    }
}

public sealed partial class ReagentThreshold : EntityConditionBase<ReagentThreshold>
{
    [DataField]
    public float Min = 0f;

    [DataField]
    public float Max = float.MaxValue;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}
