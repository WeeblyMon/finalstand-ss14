namespace Content.Shared._FinalStand.MedicalOps;

// Vanilla defibrillation stops at Critical. This carries the patient the rest of the way to their
// feet, on a fraction of their health rather than all of it.
[RegisterComponent]
public sealed partial class FSEmergencyDefibComponent : Component
{
    [DataField]
    public float ReviveHealthFraction = 0.2f;
}
