namespace Content.Shared._FinalStand.MedicalOps;

// Medic mind -> when their claim on this patient's unattributed healing expires.
[RegisterComponent]
public sealed partial class FSTreatmentAttributionComponent : Component
{
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> RecentTreaters = new();
}
