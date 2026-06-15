// SPDX-FileCopyrightText: 2024 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;

namespace Content.Shared.Chemistry.Hypospray.Events;

/// <summary>
/// Raised on the user and the target before a hypospray injects.
/// Cancel to block the injection. Set <see cref="InjectMessageOverride"/> to show a custom popup.
/// Relayed through clothing via <see cref="IInventoryRelayEvent"/>.
/// </summary>
public abstract partial class BeforeHyposprayInjectsTargetEvent : CancellableEntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid EntityUsingHypospray;
    public readonly EntityUid Hypospray;
    public EntityUid TargetGettingInjected;
    public string? InjectMessageOverride;

    protected BeforeHyposprayInjectsTargetEvent(EntityUid user, EntityUid hypospray, EntityUid target)
    {
        EntityUsingHypospray = user;
        Hypospray = hypospray;
        TargetGettingInjected = target;
    }
}

/// <summary>Raised on the user before injection.</summary>
public sealed partial class SelfBeforeHyposprayInjectsEvent : BeforeHyposprayInjectsTargetEvent
{
    public SelfBeforeHyposprayInjectsEvent(EntityUid user, EntityUid hypospray, EntityUid target)
        : base(user, hypospray, target) { }
}

/// <summary>Raised on the target before injection.</summary>
public sealed partial class TargetBeforeHyposprayInjectsEvent : BeforeHyposprayInjectsTargetEvent
{
    public TargetBeforeHyposprayInjectsEvent(EntityUid user, EntityUid hypospray, EntityUid target)
        : base(user, hypospray, target) { }
}

/// <summary>Raised on both the user and target after a successful hypospray injection.</summary>
[ByRefEvent]
public record struct AfterHyposprayInjectsEvent(EntityUid User, EntityUid Hypospray, EntityUid Target);
