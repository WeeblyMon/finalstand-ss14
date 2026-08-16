using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.CCC;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FinalStandCCCTagComponent : Component
{
    // Published from the Destructible threshold on map init, so nothing hardcodes it.
    [AutoNetworkedField]
    public float MaxHealth;
}
