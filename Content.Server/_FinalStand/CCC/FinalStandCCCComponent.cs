namespace Content.Server._FinalStand.CCC;

// marker for central command console

[RegisterComponent]
public sealed partial class FinalStandCCCComponent : Component
{
    [DataField]
    public float HealFractionPerWave = 0.06f;
}
