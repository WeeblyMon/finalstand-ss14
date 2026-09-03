namespace Content.Shared._FinalStand.MedicalOps;

// Medic mind -> when their claim on this patient's unattributed healing expires.
[RegisterComponent]
public sealed partial class FSTreatmentAttributionComponent : Component
{
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> RecentTreaters = new();

    // The chemist who made what was administered, tracked separately so they earn a smaller cut
    // without displacing the medic who actually did the work.
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> RecentSuppliers = new();
}
