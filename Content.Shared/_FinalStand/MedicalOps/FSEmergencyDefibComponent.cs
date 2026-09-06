namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent]
public sealed partial class FSEmergencyDefibComponent : Component
{
    [DataField]
    public float ReviveHealthFraction = 0.2f;
}
