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

// One shape for both vanilla and FS nodes, so the graph and detail panel render generically.
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
    public bool IsMyPersonalPick;
    public int Progress;

    public int QueuePosition;

    // Includes yourself if IsMyPersonalPick.
    public int PersonalContributorCount;

    // One color-slot index per contributor, join order - used to draw one ring per contributor.
    public List<int> ContributorSlots = new();

    public TechnologyPrototype? Vanilla;
    public FSTechNodePrototype? FsNode;

    // Materialised once instead of re-running the LINQ chain per access.
    public List<string> AllPrerequisiteIds = new();

    // The PrerequisiteGroups subset, so an "or" edge is a single lookup.
    public HashSet<string> OrPrerequisiteIds = new();

    public void BuildPrerequisiteIndex()
    {
        AllPrerequisiteIds.Clear();
        OrPrerequisiteIds.Clear();

        AllPrerequisiteIds.AddRange(Prerequisites);

        foreach (var group in PrerequisiteGroups)
        {
            foreach (var id in group)
            {
                AllPrerequisiteIds.Add(id);
                OrPrerequisiteIds.Add(id);
            }
        }
    }
}
