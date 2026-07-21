namespace Content.Shared._FinalStand.Augments;

[RegisterComponent]
public sealed partial class FSUntouchableComponent : Component
{
    public int CurrentCharges;
    public TimeSpan NextChargeTime;
}
