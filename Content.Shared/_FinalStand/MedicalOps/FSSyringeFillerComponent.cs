using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSSyringeFillerComponent : Component
{
    [DataField]
    public string SourceSlot = "source";

    [DataField]
    public string MagazineSlot = "magazine";

    [DataField]
    public string MagazineSolution = "pack";

    [DataField]
    public float UnitsPerSecond = 40f;

    [DataField, AutoNetworkedField]
    public TimeSpan? FinishAt;
}
