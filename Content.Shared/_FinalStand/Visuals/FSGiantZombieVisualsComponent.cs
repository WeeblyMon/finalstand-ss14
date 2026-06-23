using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Visuals;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class FSGiantZombieVisualsComponent : Component
{
    [DataField, AutoNetworkedField] public bool RightArmRemoved = false;
    [DataField, AutoNetworkedField] public bool LeftArmRemoved = false;
    [DataField, AutoNetworkedField] public bool HeadRemoved = false;
    [DataField, AutoNetworkedField] public bool Dead = false;
}
