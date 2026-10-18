using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mech;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMechWeaponTogglesComponent : Component
{
    [AutoNetworkedField]
    public HashSet<EntityUid> Disabled = new();
}
