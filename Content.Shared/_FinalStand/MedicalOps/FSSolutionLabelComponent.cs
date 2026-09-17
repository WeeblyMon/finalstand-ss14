using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

// Names the item after whatever is in it, so a magazine filled at the machine reads as its reagent
// rather than staying a generic "syringe magazine".
[RegisterComponent, NetworkedComponent]
public sealed partial class FSSolutionLabelComponent : Component
{
    [DataField]
    public string Solution = "pack";
}
