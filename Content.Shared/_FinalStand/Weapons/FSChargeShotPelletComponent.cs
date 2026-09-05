using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Weapons;

// carries the charge a pellet was fired at, so the client can size it to match
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class FSChargeShotPelletComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Scale = 1f;
}
