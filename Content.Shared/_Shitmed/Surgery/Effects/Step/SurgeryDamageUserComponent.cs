// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Damages the entity performing surgery (the user) when a step is completed.
// Used by xeno surgery steps to inflict acid damage on the surgeon.

using Content.Shared.Damage;
using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Medical.Surgery.Effects.Step;

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryDamageUserComponent : Component
{
    [DataField]
    public LocId? Popup;

    [DataField]
    public DamageSpecifier Damage = new();
}
