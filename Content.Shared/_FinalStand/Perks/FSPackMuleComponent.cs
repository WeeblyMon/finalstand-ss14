using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Perks;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(FSPackMuleSystem))]
public sealed partial class FSPackMuleComponent : Component
{
    [AutoNetworkedField]
    public int Level;
}
