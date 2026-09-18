namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent]
public sealed partial class FSFieldHospitalComponent : Component
{
    [ViewVariables] public float? BaseArmingDelay;
    [ViewVariables] public TimeSpan? BaseLifetime;
    [ViewVariables] public float? BaseSurgerySpeed;

}
