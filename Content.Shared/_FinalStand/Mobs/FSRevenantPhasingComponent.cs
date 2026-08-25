using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSRevenantPhasingComponent : Component
{
    [DataField]
    public float StopRange = 0.6f;
}
