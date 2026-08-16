using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.CCC;

// Client-visible marker so the CCC ready-up indicator overlay can query the
// CCC without the server-only FinalStandCCCComponent.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FinalStandCCCTagComponent : Component
{
    // Published from the Destructible threshold on map init, so nothing hardcodes it.
    [AutoNetworkedField]
    public float MaxHealth;
}
