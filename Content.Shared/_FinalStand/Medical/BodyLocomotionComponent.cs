// How many legs this body is built around, used to scale movement when legs are hurt or missing.

using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BodyLocomotionComponent : Component
{
    [DataField, AutoNetworkedField]
    public int RequiredLegs = 2;
}
