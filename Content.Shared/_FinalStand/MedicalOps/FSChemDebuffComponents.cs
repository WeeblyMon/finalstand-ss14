using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSArmorSuppressedComponent : Component
{
    [DataField]
    public TimeSpan Until;
}

[RegisterComponent, NetworkedComponent]
[Access(typeof(FSSuppressArmorRegenSystem), typeof(FSApplyVulnerabilitySystem))]
public sealed partial class FSVulnerableComponent : Component
{
    [DataField]
    public TimeSpan Until;

    [DataField]
    public float Multiplier = 1.25f;
}
