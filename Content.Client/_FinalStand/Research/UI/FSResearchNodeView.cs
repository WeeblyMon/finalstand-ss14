using System.Collections.Generic;
using System.Linq;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Research.UI;

public enum FSResearchNodeState
{
    Locked,
    Available,
    Unlocked,
}

// Unifies vanilla TechnologyPrototype and FS-authored FSTechNodePrototype into one shape the
// graph control and detail panel can render generically - needed because branches like Ordnance
// mix in cross-branch prerequisites that point back at plain vanilla nodes (e.g. Tesla Cannon
// requiring the Experimental-discipline AnomalyCoreHarnessing).
public sealed class FSResearchNodeView
{
    public required string Id;
    public required string Name;
    public required SpriteSpecifier Icon;

    /// <summary>
    /// Vanilla discipline id or FS branch id - used for rail filtering and root column grouping.
    /// </summary>
    public required string GroupId;

    public required int Tier;
    public required int Cost;

    /// <summary>
    /// Flat AND-of-all prerequisites. For FS nodes with PrerequisiteGroups too, use
    /// AllPrerequisiteIds for graph edges/layout - this alone under-represents the OR groups.
    /// </summary>
    public required List<string> Prerequisites;

    /// <summary>
    /// AND-of-OR prerequisite groups (each inner list needs only one entry unlocked). Empty for
    /// vanilla nodes and most FS nodes - only capstones with "choose one per branch" gating use it.
    /// </summary>
    public List<List<string>> PrerequisiteGroups = new();

    public required FSResearchNodeState State;

    public TechnologyPrototype? Vanilla;
    public FSTechNodePrototype? FsNode;

    /// <summary>
    /// Every prerequisite id that should draw a graph edge into this node, AND vs OR semantics
    /// collapsed - the distinction only matters for unlock-state computation, not for layout/lines.
    /// </summary>
    public IEnumerable<string> AllPrerequisiteIds => Prerequisites.Concat(PrerequisiteGroups.SelectMany(g => g));
}
