// A silo on the station's shared network. One of them is the pool that holds every material; the rest are drop-off points.
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Materials;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSiloNetworkComponent : Component
{
    [AutoNetworkedField]
    public bool Pool;
}
