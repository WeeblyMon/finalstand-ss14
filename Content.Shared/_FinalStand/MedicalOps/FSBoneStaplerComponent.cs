namespace Content.Shared._FinalStand.MedicalOps;

// Sets one fracture without the full surgery chain. The cooldown is the whole cost, so a doctor
// can answer a broken limb mid-wave but cannot walk a queue of them.
[RegisterComponent]
public sealed partial class FSBoneStaplerComponent : Component
{
    // Bone integrity restored per use. The cap is 60, so this is a partial mend, not a reset.
    [DataField]
    public float Repair = 45f;
}
