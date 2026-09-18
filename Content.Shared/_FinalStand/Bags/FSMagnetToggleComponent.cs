using Content.Shared.Inventory;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Bags;

// Right-click toggle for vanilla MagnetPickup, which has no on/off of its own. Disabling strips the
// component outright, so the settings are mirrored here to put it back exactly as it was.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSMagnetToggleComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField]
    public float Range = 1f;

    [DataField]
    public SlotFlags? SlotFlags = Inventory.SlotFlags.BELT;

    [DataField]
    public bool RequireActiveHand;

    [DataField]
    public bool Captured;
}
