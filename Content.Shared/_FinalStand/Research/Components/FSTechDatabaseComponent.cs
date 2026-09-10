using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Research.Components;

[Serializable]
public enum FSResearchTrack : byte
{
    Science,
    Medical,
}

// FS-authored counterpart to TechnologyDatabaseComponent, for FSTechNodePrototype content.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true, raiseAfterAutoHandleState: true)]
public sealed partial class FSTechDatabaseComponent : Component
{
    [AutoNetworkedField]
    [DataField]
    public FSResearchTrack Track = FSResearchTrack.Science;

    [AutoNetworkedField]
    [DataField]
    public List<ProtoId<FSTechBranchPrototype>> Branches = new();

    [AutoNetworkedField]
    [DataField]
    public List<ProtoId<FSTechNodePrototype>> UnlockedNodes = new();

    [AutoNetworkedField]
    [DataField]
    public ProtoId<FSTechNodePrototype>? ActiveResearch;

    [AutoNetworkedField]
    [DataField]
    public Dictionary<string, int> NodeProgress = new();

    [AutoNetworkedField]
    [DataField]
    public List<ProtoId<FSTechNodePrototype>> SharedQueue = new();

    [AutoNetworkedField]
    [DataField]
    public int Points;

    // Per node, one stable color-slot index per contributor - lets the client render a ring per contributor without ever sending names.
    [AutoNetworkedField]
    [DataField]
    public Dictionary<string, List<int>> PersonalContributorSlots = new();
}
