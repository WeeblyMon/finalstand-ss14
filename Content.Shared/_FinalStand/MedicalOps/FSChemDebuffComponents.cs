namespace Content.Shared._FinalStand.MedicalOps;

// Server state only, like FSVulnerableComponent: networked with no state, the client got Until = 0 and its copy of the
// shared expiry removed it every tick while each server state put it back.
[RegisterComponent]
public sealed partial class FSArmorSuppressedComponent : Component
{
    [DataField]
    public TimeSpan Until;
}

[RegisterComponent]
[Access(typeof(FSSuppressArmorRegenSystem), typeof(FSApplyVulnerabilitySystem))]
public sealed partial class FSVulnerableComponent : Component
{
    [DataField]
    public TimeSpan Until;

    [DataField]
    public float Multiplier = 1.25f;
}
