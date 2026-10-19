namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSRampageSystem))]
public sealed partial class FSRampageComponent : Component
{
    public int Stacks;
    public TimeSpan LastKillTime;
}
