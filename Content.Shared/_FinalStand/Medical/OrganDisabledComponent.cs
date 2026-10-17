using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

// Attached but not functioning, e.g. a severed or crippled limb.
[RegisterComponent, NetworkedComponent]
public sealed partial class OrganDisabledComponent : Component;
