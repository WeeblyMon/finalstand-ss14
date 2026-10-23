using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Fortifications;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSAutoCloseBarricadeComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);
}
