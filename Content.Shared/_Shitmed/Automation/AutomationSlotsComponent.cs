// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Stub: Goob automation system for device-network-controlled machines.

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Automation;

[RegisterComponent, NetworkedComponent]
public sealed partial class AutomationSlotsComponent : Component
{
    [DataField]
    public List<AutomationSlot> Slots = new();
}

[DataDefinition]
public abstract partial class AutomationSlot;

[DataDefinition]
public sealed partial class AutomatedItemSlot : AutomationSlot
{
    [DataField]
    public string? Input;

    [DataField]
    public string? Output;
}

[DataDefinition]
public sealed partial class AutomatedPorts : AutomationSlot
{
    [DataField]
    public List<string> Sinks = new();
}

[DataDefinition]
public sealed partial class AutomatedStorage : AutomationSlot
{
    [DataField]
    public string? Container;
}

[DataDefinition]
public sealed partial class AutomatedHand : AutomationSlot
{
    [DataField]
    public string? Hand;
}
