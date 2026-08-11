// An organ that can hold a small item inside it, used by cavity insertion surgery.

using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganCavityComponent : Component
{
    [DataField]
    public string ContainerName = "organ_cavity";

    [DataField, AutoNetworkedField]
    public ItemSlot Slot = new();
}
