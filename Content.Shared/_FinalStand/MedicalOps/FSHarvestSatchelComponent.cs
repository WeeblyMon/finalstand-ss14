using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSHarvestSatchelComponent : Component
{
    [DataField]
    public string Solution = "satchel";

    [DataField]
    public string Reagent = "FSBiomass";

    [DataField]
    public float Range = 8f;

    [DataField]
    public float PerKill = 4f;

    [DataField]
    public float PerWaveCap = 120f;
}
