using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSSolutionLabelComponent : Component
{
    [DataField]
    public string Solution = "pack";
}
