namespace Content.Shared._FinalStand.MedicalOps;

// Per-patient medical bookkeeping, on the body so a deleted corpse takes its ledger with it.
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
}
