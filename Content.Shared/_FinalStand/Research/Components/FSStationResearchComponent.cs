using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared._FinalStand.Research.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Research.Components;

// Server-wide singleton holding the shared research state - FSTechDatabaseComponent on each console is a synced mirror of this, see FSResearchSystem.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedFSResearchSystem))]
public sealed partial class FSStationResearchComponent : Component
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

    [DataField]
    public int WaveTrickleAmount = 500;

    // Server-only, never broadcast wholesale - each player learns only their own pick via FSPersonalResearchStateEvent.
    public Dictionary<EntityUid, ProtoId<FSTechNodePrototype>> PersonalPicks = new();
}
