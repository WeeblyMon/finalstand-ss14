// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Augments;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentActionComponent : Component
{
    [DataField]
    public EntProtoId? Action;
}
