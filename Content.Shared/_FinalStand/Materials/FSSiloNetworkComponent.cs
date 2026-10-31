using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Materials;

// One networked silo is the pool that holds every material; the rest are drop-off points.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSiloNetworkComponent : Component
{
    [AutoNetworkedField]
    public bool Pool;
}
