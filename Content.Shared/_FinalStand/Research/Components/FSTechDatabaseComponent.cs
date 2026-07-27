using Content.Shared._FinalStand.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Research.Components;

// FS-authored counterpart to TechnologyDatabaseComponent, for FSTechNodePrototype content.
// ActiveResearch/NodeProgress are inert until stage 3 wires the RP-progress-bar economy - they
// exist now so the schema and UI don't need to change again once that logic lands.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSTechDatabaseComponent : Component
{
    [AutoNetworkedField]
    [DataField]
    public List<ProtoId<FSTechNodePrototype>> UnlockedNodes = new();

    /// <summary>
    /// The single server-wide node currently receiving incoming RP, if any.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public ProtoId<FSTechNodePrototype>? ActiveResearch;

    /// <summary>
    /// Banked RP per node, keyed by node id. Persists across switching the active node.
    /// </summary>
    [AutoNetworkedField]
    [DataField]
    public Dictionary<string, int> NodeProgress = new();
}
