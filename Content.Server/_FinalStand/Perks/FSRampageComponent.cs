namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSRampageComponent : Component
{
    public int Stacks;
    public TimeSpan LastKillTime;
}
