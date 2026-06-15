// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects;

public sealed partial class TakeStaminaDamageEntityEffectSystem : EntityEffectSystem<StaminaComponent, TakeStaminaDamage>
{
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;

    protected override void Effect(Entity<StaminaComponent> entity, ref EntityEffectEvent<TakeStaminaDamage> args)
    {
        _stamina.TakeStaminaDamage(entity, args.Effect.Amount * (float)args.Scale, entity.Comp);
    }
}

/// <summary>
/// Deals or heals stamina damage. Negative Amount heals stamina.
/// </summary>
public sealed partial class TakeStaminaDamage : EntityEffectBase<TakeStaminaDamage>
{
    [DataField]
    public float Amount = -10f;

    /// <summary>Exists for YAML compatibility; not used in this implementation.</summary>
    [DataField]
    public bool Immediate;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => string.Empty;
}
