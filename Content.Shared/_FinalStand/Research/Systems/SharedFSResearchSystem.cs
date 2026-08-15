using System.Linq;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Research.Systems;

// Base for the server FSResearchSystem - shared logic so client and server can't drift.
public abstract partial class SharedFSResearchSystem : EntitySystem
{
    [Dependency] protected IPrototypeManager PrototypeManager = default!;

    public bool ArePrerequisitesMet(FSTechNodePrototype node, Func<string, bool> isUnlocked)
    {
        return node.Prerequisites.All(isUnlocked) && node.PrerequisiteGroups.All(g => g.Any(isUnlocked));
    }

    public bool IsExclusivelyBlocked(FSTechNodePrototype node, IReadOnlyCollection<string> unlockedNodeIds)
    {
        if (node.ExclusiveGroup == null)
            return false;

        foreach (var otherId in unlockedNodeIds)
        {
            if (otherId == node.ID)
                continue;
            if (PrototypeManager.TryIndex<FSTechNodePrototype>(otherId, out var other) &&
                other.ExclusiveGroup == node.ExclusiveGroup)
                return true;
        }

        return false;
    }

    public bool IsNodeUnlocked(Entity<FSStationResearchComponent?> station, string nodeId)
    {
        if (!Resolve(station, ref station.Comp, false))
            return false;

        return IsNodeUnlocked(station.Comp, nodeId);
    }

    public bool IsNodeUnlocked(FSStationResearchComponent station, string nodeId)
    {
        SyncUnlockedLookup(station);
        return station.UnlockedLookup.Contains(nodeId);
    }

    public void MarkNodeUnlocked(FSStationResearchComponent station, string nodeId)
    {
        station.UnlockedNodes.Add(nodeId);
        station.UnlockedLookup.Add(nodeId);
    }

    public void ClearUnlockedNodes(FSStationResearchComponent station)
    {
        station.UnlockedNodes.Clear();
        station.UnlockedLookup.Clear();
    }

    // Self-heals if the list was replaced wholesale, which is what applying networked state does.
    private static void SyncUnlockedLookup(FSStationResearchComponent station)
    {
        if (station.UnlockedLookup.Count == station.UnlockedNodes.Count)
            return;

        station.UnlockedLookup.Clear();
        foreach (var node in station.UnlockedNodes)
            station.UnlockedLookup.Add(node.Id);
    }
}
