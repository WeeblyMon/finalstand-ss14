using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Research.Components;

// FS-authored counterpart to TechnologyDatabaseComponent, for FSTechNodePrototype content.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSTechDatabaseComponent : Component
{
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
    public int Points;

    // Per node, one stable color-slot index per contributor - lets the client render a ring per contributor without ever sending names.
    [AutoNetworkedField]
    [DataField]
    public Dictionary<string, List<int>> PersonalContributorSlots = new();
}
