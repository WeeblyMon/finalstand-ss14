namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent]
public sealed partial class FSGravitonPullComponent : Component
{
    [DataField] public float Strength = 5f;
    [DataField] public float Range = 5f;
    [DataField] public float PulseInterval = 0.25f;
    [DataField] public double NextPulseTime;
}
