using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Research.Components;

// Which system owns a console. Science accumulates research points from combat; Medical buys nodes
// outright with department funds, so the two share a component but never share state.
[Serializable]
public enum FSResearchTrack : byte
{
    Science,
    Medical,
}

// FS-authored counterpart to TechnologyDatabaseComponent, for FSTechNodePrototype content.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSTechDatabaseComponent : Component
{
    [AutoNetworkedField]
    [DataField]
    public FSResearchTrack Track = FSResearchTrack.Science;

    // Branches this console will show. Empty means all of them, which is how the science console
    // behaved before medical got its own tree.
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
