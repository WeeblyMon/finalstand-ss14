using Robust.Shared.GameStates;
using Robust.Shared.Utility;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Shared._FinalStand.Grenades;

[Serializable, NetSerializable]
public enum GrenadeType : byte
{
    Frag,
    Incendiary,
    Flash,
    Pipe,
}

/// <summary>
/// Attached to the persistent grenade pack item sitting in the player's inventory.
/// Tracks stock and upgrade state for one grenade type.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSGrenadePackComponent : Component
{
    [DataField, AutoNetworkedField] public GrenadeType PackType = GrenadeType.Frag;
    [DataField, AutoNetworkedField] public int Stock = 3;
    [DataField, AutoNetworkedField] public int MaxStock = 3;
    [DataField, AutoNetworkedField] public int RegenPerWave = 1;

    /// <summary>Prototype to spawn when a grenade from this pack is thrown.</summary>
    [DataField] public EntProtoId GrenadeProtoId = "FSGrenadeFrag";

    /// <summary>Duration of incendiary fire zone (seconds). Upgraded via GrenadeBurnDuration.</summary>
    [DataField, AutoNetworkedField] public float BurnDuration = 5f;

    /// <summary>Duration of flash stun (seconds). Upgraded via GrenadeStunDuration.</summary>
    [DataField, AutoNetworkedField] public float StunDuration = 3f;

    /// <summary>Duration of bait decoy (seconds). Upgraded via GrenadeBaitDuration.</summary>
    [DataField, AutoNetworkedField] public float BaitDuration = 8f;

    /// <summary>If true, grenade explodes immediately on landing instead of using timer fuse.</summary>
    [DataField, AutoNetworkedField] public bool ImpactFuse = false;

    /// <summary>Bonus radius added to fire zone / stun radius effects. Upgraded via GrenadeEffectRadius.</summary>
    [DataField, AutoNetworkedField] public float EffectRadius = 0f;

    /// <summary>Bonus frag blast intensity multiplier. Upgraded via GrenadeBlastBonus.</summary>
    [DataField, AutoNetworkedField] public float BlastBonus = 0f;

    /// <summary>If true, grenade splits into sub-munitions on detonation. Upgraded via GrenadeCluster.</summary>
    [DataField, AutoNetworkedField] public bool IsCluster = false;

    /// <summary>If true, the pack throws a gravaton bomb instead of its normal grenade.</summary>
    [DataField, AutoNetworkedField] public bool IsSingularity = false;

    /// <summary>Sprite the pack switches to once IsSingularity is set.</summary>
    [DataField] public SpriteSpecifier? SingularitySprite;

    /// <summary>The selection action entity granted when this pack is first bought.</summary>
    [DataField] public EntProtoId? SelectActionProtoId;
    [DataField] public EntityUid? GrantedActionId;
}

/// <summary>
/// Placed on the player to track which grenade type is currently active for quick-throw.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSActiveGrenadeComponent : Component
{
    [DataField, AutoNetworkedField] public GrenadeType ActiveType = GrenadeType.Frag;
}

/// <summary>
/// Placed on a grenade-select action entity to identify which grenade type it selects.
/// Read client-side by ActionButton to determine highlight state.
/// </summary>
[RegisterComponent]
public sealed partial class FSGrenadeSelectActionComponent : Component
{
    [DataField] public GrenadeType GrenadeType = GrenadeType.Frag;
}

// ─── Selection action events ─────────────────────────────────────────────────

public sealed partial class FSSelectFragGrenadeEvent : InstantActionEvent { }
public sealed partial class FSSelectIncendiaryGrenadeEvent : InstantActionEvent { }
public sealed partial class FSSelectFlashGrenadeEvent : InstantActionEvent { }
public sealed partial class FSSelectPipeGrenadeEvent : InstantActionEvent { }

/// <summary>
/// Marker placed on FSBaitDecoy entities. Signals FSBaitAttractSystem to aggro nearby zombies.
/// </summary>
[RegisterComponent]
public sealed partial class FSBaitDecoyComponent : Component { }
