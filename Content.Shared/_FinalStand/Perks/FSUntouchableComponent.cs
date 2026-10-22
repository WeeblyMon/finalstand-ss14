namespace Content.Shared._FinalStand.Perks;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FSUntouchableComponent : Component
{
    public int CurrentCharges;
    [AutoPausedField] public TimeSpan NextChargeTime;
}
