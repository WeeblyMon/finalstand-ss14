namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSTreatmentAttributionSystem))]
public sealed partial class FSTreatmentAttributionComponent : Component
{
    [ViewVariables, AutoPausedField]
    public Dictionary<EntityUid, TimeSpan> RecentTreaters = new();

    [ViewVariables, AutoPausedField]
    public Dictionary<EntityUid, TimeSpan> RecentSuppliers = new();
}
