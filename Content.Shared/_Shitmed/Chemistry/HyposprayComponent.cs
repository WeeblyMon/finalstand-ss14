// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed.Chemistry;

/// <summary>
/// Marks a hypospray for instant, no-do-after injection into mobs.
/// Works alongside <see cref="InjectorComponent"/>; HypospraySystem handles mob
/// interactions first and sets args.Handled, so InjectorSystem won't fire its do-after.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HyposprayComponent : Component
{
    /// <summary>Which solution container to draw from when injecting.</summary>
    [DataField]
    public string SolutionName = "hypospray";

    /// <summary>Units transferred per use.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 TransferAmount = FixedPoint2.New(5);

    /// <summary>If true, can only inject into mobs (entities with a bloodstream); cannot refill containers.</summary>
    [DataField, AutoNetworkedField]
    public bool OnlyAffectsMobs = true;

    /// <summary>If true, cannot draw from containers — injection only.</summary>
    [DataField]
    public bool InjectOnly = false;

    [DataField]
    public SoundSpecifier InjectSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    /// <summary>Cached solution reference.</summary>
    [ViewVariables]
    public Entity<SolutionComponent>? CachedSolution = null;
}
