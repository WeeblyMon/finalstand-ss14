// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Grants components to the wearer when clothing is equipped (e.g. surgical gloves grant SurgeryIgnoreClothing).

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Surgery;

[RegisterComponent, NetworkedComponent, ComponentProtoName("ClothingGrantComponent")]
public sealed partial class ClothingGrantComponent : Component
{
    [DataField]
    public ComponentRegistry? Component;
}
