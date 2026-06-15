// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Marker: identifies a tissue sample item or surgery step tool.

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Surgery;

[RegisterComponent, NetworkedComponent]
public sealed partial class TissueSampleComponent : Component;

[RegisterComponent, NetworkedComponent]
public sealed partial class HasTissueSampleComponent : Component;
