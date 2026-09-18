namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent]
public sealed partial class FSMedicalPatientComponent : Component
{
    [ViewVariables]
    public Dictionary<EntityUid, float> HealedSinceReset = new();

    [ViewVariables]
    public float DamageSinceReset;

    [ViewVariables]
    public EntityUid? PendingSaveCredit;

    [ViewVariables]
    public int PendingSaveWave;

    [ViewVariables]
    public TimeSpan PendingSaveAt;
}
