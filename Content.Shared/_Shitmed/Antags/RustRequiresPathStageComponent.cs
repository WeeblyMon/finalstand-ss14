// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Antags;

[RegisterComponent, NetworkedComponent]
public sealed partial class RustRequiresPathStageComponent : Component
{
    [DataField]
    public int PathStage;
}
