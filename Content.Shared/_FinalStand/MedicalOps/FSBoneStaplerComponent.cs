namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent]
public sealed partial class FSBoneStaplerComponent : Component
{
    [ViewVariables] public TimeSpan? BaseUseDelay;

    [DataField]
    public float Repair = 45f;
}
