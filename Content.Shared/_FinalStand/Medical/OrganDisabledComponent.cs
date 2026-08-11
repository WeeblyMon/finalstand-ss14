// Marks an organ as attached but not functioning, e.g. a severed or crippled limb.

using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

[RegisterComponent, NetworkedComponent]
public sealed partial class OrganDisabledComponent : Component;
