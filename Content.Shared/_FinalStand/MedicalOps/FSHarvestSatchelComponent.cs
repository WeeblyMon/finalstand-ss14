using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FSHarvestSatchelComponent : Component
{
    [ViewVariables] public float? BaseRange;
    [ViewVariables] public float? BasePerKill;
    [ViewVariables] public float? BasePerWaveCap;

    [DataField]
    public string Solution = "satchel";

    [DataField]
    public string Reagent = "FSBiomass";

    [DataField]
    public float Range = 5f;

    [DataField]
    public float PerKill = 4f;

    [DataField]
    public float PerWaveCap = 120f;

    [AutoNetworkedField]
    public float AccruedThisWave;

    [AutoNetworkedField]
    public int TrackedWave = -1;
}
