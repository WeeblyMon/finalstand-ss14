using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

// Used by cavity insertion surgery.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganCavityComponent : Component
{
    [DataField]
    public string ContainerName = "organ_cavity";

    [DataField, AutoNetworkedField]
    public ItemSlot Slot = new();
}
