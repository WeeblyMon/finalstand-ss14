namespace Content.Shared._FinalStand.Ailments;

[DataDefinition]
public sealed partial class FSInjurySpec
{
    [DataField]
    public int Bleed;

    [DataField]
    public bool Fracture;

    [DataField]
    public bool Burn;

    [DataField]
    public bool Arrhythmia;

    [DataField]
    public bool Poison;

    [DataField]
    public bool IgnoreFloor;

    [DataField]
    public Dictionary<FSAilmentMeter, float> Meters = new();
}
