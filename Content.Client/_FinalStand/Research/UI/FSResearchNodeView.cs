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
    ExclusivelyBlocked,
}

// Unifies vanilla TechnologyPrototype and FS-authored FSTechNodePrototype into one shape for the graph control and detail panel to render generically.
public sealed class FSResearchNodeView
{
    public required string Id;
    public required string Name;
    public required SpriteSpecifier Icon;

    public required string GroupId;

    public required int Tier;
    public required int Cost;

    public required List<string> Prerequisites;

    public List<List<string>> PrerequisiteGroups = new();

    public required FSResearchNodeState State;

    public bool IsActiveResearch;
    public int Progress;

    public TechnologyPrototype? Vanilla;
    public FSTechNodePrototype? FsNode;

    public IEnumerable<string> AllPrerequisiteIds => Prerequisites.Concat(PrerequisiteGroups.SelectMany(g => g));
}
