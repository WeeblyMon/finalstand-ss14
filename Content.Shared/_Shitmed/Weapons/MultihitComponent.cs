// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Shitmed.Weapons;

[RegisterComponent, NetworkedComponent]
public sealed partial class MultihitComponent : Component
{
    [DataField]
    public EntityWhitelist? MultihitWhitelist;

    [DataField]
    public List<MultihitCondition> Conditions = new();
}

[ImplicitDataDefinitionForInheritors]
[Serializable, NetSerializable]
public abstract partial class MultihitCondition;

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class MultihitUserWhitelistEvent : MultihitCondition
{
    [DataField]
    public EntityWhitelist? Whitelist;
}
