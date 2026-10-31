using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
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

    [ViewVariables] public float? BaseUnitsPerSecond;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan? FinishAt;

    [DataField, AutoNetworkedField, AutoPausedField]
    public TimeSpan StartedAt;
}
