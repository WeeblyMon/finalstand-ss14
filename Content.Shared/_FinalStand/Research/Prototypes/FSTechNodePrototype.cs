using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._FinalStand.Research.Prototypes;

// FS-authored equivalent of TechnologyPrototype, for branches (Ordnance, and later Bulwark/
// Logistics/Aberrant) whose unlock model doesn't fit vanilla's per-discipline tier-percentage
// gate: wave-gating, server-wide RP-progress-bar research, and cross-branch prerequisite joins
// (e.g. Tesla Cannon requiring an Experimental-discipline vanilla node).
//
// Prerequisites is untyped string ids (not ProtoId<FSTechNodePrototype>) on purpose - a
// prerequisite can point at either another FSTechNodePrototype or a vanilla TechnologyPrototype,
// and is AND-gated (all must be unlocked), matching vanilla's TechnologyPrerequisites semantics.
[Prototype("fsTechNode")]
public sealed partial class FSTechNodePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = string.Empty;

    [DataField(required: true)]
    public SpriteSpecifier Icon = default!;

    [DataField(required: true)]
    public ProtoId<FSTechBranchPrototype> Branch;

    [DataField(required: true)]
    public int Tier;

    /// <summary>
    /// Minimum wave number this node becomes available at. 0 = available from round start.
    /// </summary>
    [DataField]
    public int WaveGate;

    [DataField]
    public int Cost = 5000;

    [DataField]
    public List<string> Prerequisites = new();

    /// <summary>
    /// AND-of-OR prerequisite groups, layered on top of Prerequisites (plain AND-of-all). Each
    /// inner list needs at least one entry unlocked. Used for capstones that need "at least one
    /// choice from each branch" rather than every single node in every branch.
    /// </summary>
    [DataField]
    public List<List<string>> PrerequisiteGroups = new();

    /// <summary>
    /// Nodes sharing the same non-null tag are mutually exclusive - unlocking one is meant to
    /// permanently lock out the others in the same group. Enforcement is stage-3 (needs a real
    /// unlock system); this field just carries the design intent for now.
    /// </summary>
    [DataField]
    public string? ExclusiveGroup;

    /// <summary>
    /// Player-facing description of the small team-wide buff this node grants.
    /// </summary>
    [DataField]
    public LocId BonusDescription = string.Empty;

    /// <summary>
    /// Shop entity this node gates purchase access to, if any. Null for pure-bonus nodes.
    /// </summary>
    [DataField]
    public EntProtoId? WeaponShopUnlock;

    [DataField]
    public bool Hidden;
}
