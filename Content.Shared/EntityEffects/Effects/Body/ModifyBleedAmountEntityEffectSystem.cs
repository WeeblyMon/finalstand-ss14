// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.EntityEffects.Effects.Body;

public sealed partial class ModifyBleedAmountEntityEffectSystem : EntityEffectSystem<BloodstreamComponent, ModifyBleedAmount>
{
    [Dependency] private BloodstreamSystem _bloodstream = default!;

    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<ModifyBleedAmount> args)
    {
        _bloodstream.TryModifyBleedAmount(entity.AsNullable(), args.Effect.Amount * args.Scale);
    }
}

public sealed partial class ModifyBleedAmount : EntityEffectBase<ModifyBleedAmount>
{
    [DataField]
    public float Amount = -1.0f;

    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => string.Empty;
}
