using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Repair;

// Max health sent to clients so engineers can see a health bar over player-built structures.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSStructureHealthComponent : Component
{
    [AutoNetworkedField]
    public float Max;
}
