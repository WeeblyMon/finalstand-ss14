using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSSyringeGunComponent : Component
{
    [DataField]
    public string MagazineSolution = "pack";

    [DataField]
    public string DartSolution = "injector";
}
