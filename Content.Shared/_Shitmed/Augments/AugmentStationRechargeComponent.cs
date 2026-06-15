// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Added to body entity via Organ.OnAdd when a station recharger augment is implanted.

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Augments;

[RegisterComponent, NetworkedComponent]
public sealed partial class AugmentStationRechargeComponent : Component;
