namespace Content.Server._FinalStand.NPC;

/// <summary>
/// Directional contingent a zombie belongs to. Hard-assigned at spawn from the spawner's
/// DirectionLabel. Phase 2+ HordeBrain contingent reassignments broadcast by FrontId, not
/// by SpawnerUid, so this is the key the contingent logic keys on.
/// </summary>
public enum HordeFront : byte
{
    Unknown = 0,
    North,
    East,
    South,
    West,
}

/// <summary>
/// Set at spawn time. FrontId is the primary contingent key. SpawnerUid is retained for
/// debug/logging only.
/// </summary>
[RegisterComponent]
public sealed partial class FSFrontAffinityComponent : Component
{
    [DataField]
    public HordeFront FrontId;

    [DataField]
    public EntityUid SpawnerUid;
}
