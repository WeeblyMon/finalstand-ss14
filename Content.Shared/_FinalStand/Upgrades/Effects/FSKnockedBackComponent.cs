namespace Content.Shared._FinalStand.Upgrades.Effects;

[RegisterComponent]
public sealed partial class FSKnockedBackComponent : Component
{
    public TimeSpan EndTime;
    public bool InputMoverRemoved;
}
