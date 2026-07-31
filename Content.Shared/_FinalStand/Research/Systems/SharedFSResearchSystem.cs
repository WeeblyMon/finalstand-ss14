using System.Linq;
using Content.Shared._FinalStand.Research.Components;
using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Research.Systems;

// Base for the server FSResearchSystem - shared logic so client and server can't drift.
public abstract class SharedFSResearchSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;

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
        return Resolve(station, ref station.Comp, false) && station.Comp.UnlockedNodes.Any(u => u.Id == nodeId);
    }
}
