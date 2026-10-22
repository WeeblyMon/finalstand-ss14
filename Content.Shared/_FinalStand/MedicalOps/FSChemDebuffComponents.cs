namespace Content.Shared._FinalStand.MedicalOps;

// Server state only, like FSVulnerableComponent: networked with no state, the client got Until = 0 and its copy of the
// shared expiry removed it every tick while each server state put it back.
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FSArmorSuppressedComponent : Component
{
    [DataField, AutoPausedField]
    public TimeSpan Until;
}

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSSuppressArmorRegenSystem), typeof(FSApplyVulnerabilitySystem))]
public sealed partial class FSVulnerableComponent : Component
{
    [DataField, AutoPausedField]
    public TimeSpan Until;

    [DataField]
    public float Multiplier = 1.25f;
}
