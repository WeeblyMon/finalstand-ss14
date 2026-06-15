// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Stub: checks if an entity has (or lacks) a specific organ metabolizer type.
// Used for species-specific reagent effects (e.g. Yowie metabolizer bypass).

using Content.Shared.Body.Components;
using Content.Shared.EntityConditions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.EntityConditions;

public sealed partial class OrganTypeConditionSystem : EntityConditionSystem<BloodstreamComponent, OrganType>
{
    protected override void Condition(Entity<BloodstreamComponent> entity, ref EntityConditionEvent<OrganType> args)
    {
        // Stub: treat all entities as not having any special organ type.
        // If shouldHave is false (condition: entity does NOT have this type), pass.
        // If shouldHave is true (condition: entity DOES have this type), fail.
        args.Result = !args.Condition.ShouldHave;
    }
}

public sealed partial class OrganType : EntityConditionBase<OrganType>
{
    [DataField]
    public string Type = string.Empty;

    [DataField]
    public bool ShouldHave = true;

    public override string EntityConditionGuidebookText(IPrototypeManager prototype) => string.Empty;
}
