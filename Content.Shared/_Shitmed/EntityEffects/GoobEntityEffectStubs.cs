// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Stubs for Goob-Station entity effects not present in vanilla SS14.

using Content.Shared.Chemistry.Reagent;
using Content.Shared.EntityEffects;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.EntityEffects;

/// <summary>
/// Stub: modifies disease progression. Requires a full disease system.
/// </summary>
public sealed partial class DiseaseProgressChange : EntityEffectBase<DiseaseProgressChange>
{
    [DataField]
    public string AffectedType = string.Empty;

    [DataField]
    public float ProgressModifier = -0.1f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => string.Empty;
}

/// <summary>
/// Stub: modifies immunity gain rate. Requires a full immunity system.
/// </summary>
public sealed partial class ImmunityModifier : EntityEffectBase<ImmunityModifier>
{
    [DataField]
    public float GainRateModifier;

    [DataField]
    public float StrengthModifier;

    [DataField]
    public float StatusLifetime;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => string.Empty;
}

/// <summary>
/// Goob: direct movement speed modifier applied per metabolism tick. Used in BZ for slime player interactions.
/// No-op stub — only affects slime-type organics interacting with NitrosylPlasmide, which doesn't exist in this fork.
/// </summary>
public sealed partial class MovespeedModifier : EntityEffectBase<MovespeedModifier>
{
    [DataField] public float WalkSpeedModifier = 1f;
    [DataField] public float SprintSpeedModifier = 1f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => string.Empty;
}

/// <summary>
/// Goob: Nitrium-specific movement speed modifier. Applies a tiered speed boost based on how much
/// Nitrium is in the body. Implemented using the standard ReagentSpeed status effect.
/// </summary>
public sealed partial class NitriumMovespeedModifier : EntityEffectBase<NitriumMovespeedModifier>
{
    [DataField] public float WalkSpeedModifier = 1f;
    [DataField] public float SprintSpeedModifier = 1f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => string.Empty;
}

public sealed partial class NitriumMovespeedModifierSystem : EntityEffectSystem<MovementSpeedModifierComponent, NitriumMovespeedModifier>
{
    [Dependency] private MovementModStatusSystem _movementMod = default!;

    protected override void Effect(Entity<MovementSpeedModifierComponent> entity, ref EntityEffectEvent<NitriumMovespeedModifier> args)
    {
        // Keep the modifier active for 3 s per tick; metabolism fires every ~2 s so it stays sustained.
        _movementMod.TryAddMovementSpeedModDuration(
            entity,
            MovementModStatusSystem.ReagentSpeed,
            TimeSpan.FromSeconds(3),
            args.Effect.WalkSpeedModifier,
            args.Effect.SprintSpeedModifier);
    }
}

/// <summary>
/// Goob: adds a reagent directly to the entity's bloodstream. Used by Nitrium to add NitrosylPlasmide.
/// No-op stub — NitrosylPlasmide has no prototype in this fork so there is nothing useful to add.
/// </summary>
public sealed partial class AddReagentToBlood : EntityEffectBase<AddReagentToBlood>
{
    [DataField] public ProtoId<ReagentPrototype>? Reagent;
    [DataField] public float Amount = 1f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => string.Empty;
}
