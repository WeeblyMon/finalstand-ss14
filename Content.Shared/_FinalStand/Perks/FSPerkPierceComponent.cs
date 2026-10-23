using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Perks;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSPerkPierceComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Level;
}
