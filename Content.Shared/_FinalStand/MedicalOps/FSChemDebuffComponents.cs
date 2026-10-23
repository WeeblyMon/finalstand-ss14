using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.MedicalOps;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSVulnerableComponent : Component
{
    [DataField]
    public TimeSpan Until;

    [DataField]
    public float Multiplier = 1.25f;
}

[RegisterComponent]
public sealed partial class FSChemSlowedComponent : Component
{
    [DataField]
    public TimeSpan Until;

    [DataField]
    public float Multiplier = 0.6f;
}
