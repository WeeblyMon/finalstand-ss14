// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Applied to hand body parts to modify do-after duration (e.g. D.E.X cybernetic hands: 0.9x).

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.DoAfter;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class DoAfterDelayMultiplierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Multiplier = 1f;
}
