namespace Content.Shared._FinalStand.MedicalOps;

// Marks the pitched tent. Exists so research can tune its deploy time and lifetime without a second
// subscription to MapInit on FSArmingDelayComponent or FSDeployableLifetimeComponent, which
// FSArmingDelaySystem and FSDeployableLifetimeSystem already own.
[RegisterComponent]
public sealed partial class FSFieldHospitalComponent : Component
{
    [ViewVariables] public float? BaseArmingDelay;
    [ViewVariables] public TimeSpan? BaseLifetime;
    [ViewVariables] public float? BaseSurgerySpeed;

}
