using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

// Leg count the body is built around, used to scale movement when legs are hurt or missing.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyLocomotionComponent : Component
{
    [DataField, AutoNetworkedField]
    public int RequiredLegs = 2;
}
