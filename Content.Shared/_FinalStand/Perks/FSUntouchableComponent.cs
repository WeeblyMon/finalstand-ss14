namespace Content.Shared._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSUntouchableComponent : Component
{
    public int CurrentCharges;
    public TimeSpan NextChargeTime;
}
