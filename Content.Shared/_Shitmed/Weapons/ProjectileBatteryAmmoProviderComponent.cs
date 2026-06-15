// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Stub: provides battery-powered projectile ammo for weapons like the decloner/alien gun.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Weapons;

[RegisterComponent, NetworkedComponent]
public sealed partial class ProjectileBatteryAmmoProviderComponent : Component
{
    [DataField]
    public EntProtoId Proto = default!;

    [DataField]
    public float FireCost = 100f;
}
