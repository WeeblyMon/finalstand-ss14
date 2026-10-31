using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mech;

// Mounted weapons the pilot has switched off; everything else fires together.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(FSMechWeaponSelectSystem))]
public sealed partial class FSMechWeaponTogglesComponent : Component
{
    [DataField, AutoNetworkedField]
    public HashSet<EntityUid> Disabled = new();
}
