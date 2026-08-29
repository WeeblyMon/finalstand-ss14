// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Stub components for Goob-Station features not yet ported to Final Stand.
// All are empty markers or field-only — no systems process them.

using Robust.Shared.GameStates;

namespace Content.Shared._Shitmed;

// ── Stamina system extensions ────────────────────────────────────────────────

/// <summary>Stub: modifies stamina capacity. Needs StaminaModifierSystem.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StaminaModifierComponent : Component
{
    [DataField] public float StaminaModifier = 0f;

    [DataField] public float Modifier = 1f;
}

/// <summary>Stub: modifies stamina regeneration rate. Needs StaminaRegenerationSystem.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StaminaRegenerationComponent : Component
{
    [DataField] public float StaminaRegen = 1f;

    [DataField] public float RegenerationRate = 1f;
}

// ── Movement / map interaction stubs ────────────────────────────────────────

/// <summary>Stub: entity can crawl through ventilation ducts. Needs VentCrawlerSystem.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class VentCrawlerComponent : Component
{
    [DataField] public bool AllowInventory = true;
}

/// <summary>Stub: entity is immune to step triggers (traps, pressure plates).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StepTriggerImmuneComponent : Component;

// ── Language / accent stubs ──────────────────────────────────────────────────

/// <summary>Stub: entity speaks with an Ohio accent. No system — purely cosmetic.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OhioAccentComponent : Component;

// ── Exoskeleton surgery step state markers ──────────────────────────────────

/// <summary>Marker: exoskeleton incision has been made during surgery.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ExoskeletonIncisionComponent : Component;

/// <summary>Marker: exoskeleton is open (incision widened) during surgery.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ExoskeletonOpenComponent : Component;

/// <summary>Marker: exoskeleton has been sawed through during surgery.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ExoskeletonSawedComponent : Component;

// ── Plasma alien body part stubs ─────────────────────────────────────────────

/// <summary>Stub: marks a plasma alien vessel body part.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlasmaVesselComponent : Component;

/// <summary>Stub: marks a plasma alien severed limb.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PlasmaSeveredComponent : Component;

// ── Xenobiology / creature stubs ─────────────────────────────────────────────

/// <summary>Stub: entity is a xenomorph mob.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenomorphComponent : Component
{
    [DataField] public string? Caste;
}

/// <summary>Stub: entity is compatible with xenomorph biology (can be implanted, etc).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenoCompatibleComponent : Component;

/// <summary>Stub: entity is a xenomorph body part.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenoBodyPartComponent : Component;

/// <summary>Stub: entity produces acid for attacks.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AcidGlandComponent : Component;

/// <summary>Stub: marker that acid on this entity has been neutralized.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class AcidNeutralizedComponent : Component;

/// <summary>Stub: entity spins resin webs (xenomorph).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ResinSpinnerComponent : Component;

/// <summary>Stub: entity is a xeno hive node connection point.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HiveNodeComponent : Component;

/// <summary>Stub: entity produces neurotoxin for attacks.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class NeurotoxinGlandComponent : Component;

/// <summary>Stub: entity lays eggs.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EggSackComponent : Component;

/// <summary>Stub: entity drinks battery charge as sustenance.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class BatteryDrinkerComponent : Component;

// ── Weapon stubs ──────────────────────────────────────────────────────────────

/// <summary>Stub: hitscan weapon that uses battery charge instead of ammo.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HitscanBatteryAmmoProviderComponent : Component
{
    [DataField] public float EnergyPerShot = 50f;
    [DataField] public string FireSound = string.Empty;

    [DataField] public string? Proto;
    [DataField] public float FireCost = 50f;
}
