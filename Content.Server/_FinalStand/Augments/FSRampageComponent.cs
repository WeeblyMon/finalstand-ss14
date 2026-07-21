namespace Content.Server._FinalStand.Augments;

[RegisterComponent]
public sealed partial class FSRampageComponent : Component
{
    public int Stacks;
    public TimeSpan LastKillTime;
}
