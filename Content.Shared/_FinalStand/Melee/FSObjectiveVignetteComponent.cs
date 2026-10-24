namespace Content.Shared._FinalStand.Melee;

[RegisterComponent]
public sealed partial class FSObjectiveVignetteComponent : Component
{
    [DataField]
    public Color Color = Color.FromHex("#3fb8c8");

    [DataField]
    public float Strength = 0.15f;
}
