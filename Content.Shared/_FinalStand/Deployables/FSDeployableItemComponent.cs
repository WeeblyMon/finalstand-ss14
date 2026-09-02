using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Deployables;

// On the held/purchasable item. Placing it plants DeployedProtoId and consumes one Stock (regenerates via FSDeployableRegenSystem).
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
    public int RegenPerWave = 1;

    // Waves between refills; 1 tops up every prep, 2 every other prep.
    [DataField]
    public int WavesPerRegen = 1;

    public int WavesSinceRegen;
}
