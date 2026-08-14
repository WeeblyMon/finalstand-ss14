// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Body;

public sealed partial class ModifyBleedAmountEntityEffectSystem : EntityEffectSystem<BloodstreamComponent, ModifyBleedAmount>
{
    [Dependency] private WoundSystem _wounds = default!;
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<ModifyBleedAmount> args)
    {
        var amount = args.Effect.Amount * args.Scale;
        _bloodstream.TryModifyBleedAmount(entity.AsNullable(), amount);

        if (amount < 0)
            _wounds.TryHealBleedsOnBody(entity.Owner, amount);
    }
}

public sealed partial class ModifyBleedAmount : EntityEffectBase<ModifyBleedAmount>
{
    [DataField]
    public float Amount = -1.0f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => string.Empty;
}
