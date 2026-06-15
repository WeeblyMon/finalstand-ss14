// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Stub: disease injection pen component. Injects a live disease instead of a vaccine.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Disease;

[RegisterComponent, NetworkedComponent]
public sealed partial class DiseasePenComponent : Component
{
    [DataField]
    public bool Vaccine = true;
}

[Serializable, NetSerializable]
public enum DiseasePenVisuals : byte
{
    Used,
}
