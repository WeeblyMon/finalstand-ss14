// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class ModifyDelayedKnockdownComponent : Component
{
    [DataField]
    public bool Cancel;

    [DataField]
    public float DelayDelta;

    [DataField]
    public float KnockdownTimeDelta;
}
