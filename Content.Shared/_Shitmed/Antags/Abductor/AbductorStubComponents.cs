// SPDX-FileCopyrightText: 2026 FinalStand Contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Antags.Abductor;

// These are stubs. They carry no behaviour — they exist so the abductor prototypes load. The
// datafields mirror Goob's real components (same names and defaults) so the authored values survive
// until the features are ported; Goob's own types (CollectiveMindPrototype, GrabStage) are not in
// this fork, so the closest primitive is used instead.

/// <summary>Stub — satisfies type: Absorbable in abductor.yml.</summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class AbsorbableComponent : Component;

/// <summary>Stub — satisfies type: CollectiveMind in abductor.yml.</summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class CollectiveMindComponent : Component
{
    [DataField]
    public string? DefaultChannel;

    [DataField]
    public HashSet<string> Channels = new();
}

/// <summary>Stub — satisfies type: GrabIntent in abductor.yml.</summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class GrabIntentComponent : Component
{
    [DataField]
    public TimeSpan StageChangeCooldown = TimeSpan.FromSeconds(1f);

    [DataField]
    public float GrabThrowDamageModifier = 2f;

    [DataField]
    public float GrabThrownSpeed = 7f;

    [DataField]
    public float SoftGrabSpeedModifier = 0.9f;

    [DataField]
    public float HardGrabSpeedModifier = 0.7f;

    [DataField]
    public float ChokeGrabSpeedModifier = 0.4f;
}

/// <summary>Stub — satisfies type: Grabbable in abductor.yml.</summary>
[NetworkedComponent, RegisterComponent]
public sealed partial class GrabbableComponent : Component;
