namespace Content.Shared._FinalStand.Weapons;

[RegisterComponent]
public sealed partial class FSGravitonCoreComponent : Component
{
    [DataField] public int Level = 1;
    [DataField] public float PullStrengthBase = 5f;
    [DataField] public float MaxRangeBase = 4f;
}
