namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent]
public sealed partial class FSTreatmentAttributionComponent : Component
{
    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> RecentTreaters = new();

    [ViewVariables]
    public Dictionary<EntityUid, TimeSpan> RecentSuppliers = new();
}
