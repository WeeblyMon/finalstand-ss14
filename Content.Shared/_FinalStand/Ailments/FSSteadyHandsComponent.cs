using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Ailments;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSSteadyHandsComponent : Component
{
    [DataField]
    public float DelayMultiplier = 0.5f;
}
