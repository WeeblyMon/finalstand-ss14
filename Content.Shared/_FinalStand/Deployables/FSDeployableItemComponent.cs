using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSDeployableItemComponent : Component
{
    [DataField(required: true)]
    public EntProtoId DeployedProtoId = default!;

    [DataField, AutoNetworkedField]
    public int Stock = 1;

    [DataField, AutoNetworkedField]
    public int MaxStock = 1;

    [DataField]
    public bool RequiresScience = true;

    [DataField]
    public bool RequiresMedical;

    [DataField]
    public int RegenPerWave = 1;

    [DataField]
    public int WavesPerRegen = 1;

    public int WavesSinceRegen;
}
