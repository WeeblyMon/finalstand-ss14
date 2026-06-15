// SPDX-FileCopyrightText: 2025 DeltaV contributors (original), 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Stub: EmpFlash is a DeltaV Cosmic Cult effect that flashes cult members when hit
// with holy water. Cosmic Cult is not in Final Stand — this is a no-op stub.

using Content.Shared.Body.Components;
using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.EntityEffects;

public sealed partial class EmpFlashEntityEffectSystem : EntityEffectSystem<BloodstreamComponent, EmpFlash>
{
    protected override void Effect(Entity<BloodstreamComponent> entity, ref EntityEffectEvent<EmpFlash> args) { }
}

/// <summary>
/// Stub: DeltaV Cosmic Cult effect. No-op in Final Stand (no cult system).
/// </summary>
public sealed partial class EmpFlash : EntityEffectBase<EmpFlash>
{
    public override string EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => string.Empty;
}
