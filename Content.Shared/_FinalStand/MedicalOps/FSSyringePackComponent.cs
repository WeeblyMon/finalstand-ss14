using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSSyringePackComponent : Component
{
    [DataField]
    public string Container = "storagebase";

    [DataField]
    public string SyringeSolution = "injector";
}
