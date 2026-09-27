using System.Collections.Generic;
using Content.Shared._FinalStand.Research.Prototypes;

namespace Content.Client._FinalStand.Research.UI;

public enum FSResearchNodeState
{
    Locked,
    Available,
    Unlocked,
    ExclusivelyBlocked,
}

public sealed class FSResearchNodeView
{
    public FSTechNodePrototype Proto { get; }
    public List<string> AllPrerequisiteIds { get; } = new();
    public HashSet<string> OrPrerequisiteIds { get; } = new();

    public FSResearchNodeState State = FSResearchNodeState.Locked;
    public bool IsActiveResearch;
    public bool IsMyPersonalPick;
    public int Progress;
    public int QueuePosition;
    public int PersonalContributorCount;

    public FSResearchNodeView(FSTechNodePrototype proto)
    {
        Proto = proto;
        AllPrerequisiteIds.AddRange(proto.Prerequisites);
        foreach (var group in proto.PrerequisiteGroups)
        {
            AllPrerequisiteIds.AddRange(group);
            OrPrerequisiteIds.UnionWith(group);
        }
    }

    public string Id => Proto.ID;
    public string Name => Proto.Name;
    public string GroupId => Proto.Branch.Id;
    public int Tier => Proto.Tier;
    public int Cost => Proto.Cost;
    public bool IsCapstone => Proto.WeaponShopUnlock != null;
    public bool IsDone => State == FSResearchNodeState.Unlocked;
    public bool IsActive => !IsDone && (IsActiveResearch || IsMyPersonalPick);
    public float ProgressFraction => Cost > 0 ? Math.Clamp((float) Progress / Cost, 0f, 1f) : 0f;
}
