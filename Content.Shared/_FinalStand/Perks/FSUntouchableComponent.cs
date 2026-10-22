namespace Content.Shared._FinalStand.Perks;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FSUntouchableComponent : Component
{
    public int CurrentCharges;
    // Null while no recharge is pending; the pause shift leaves null alone, where it moved a zero off its meaning.
    [AutoPausedField] public TimeSpan? NextChargeTime;
}
