namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSDeathAuraSystem))]
public sealed partial class FSDeathAuraComponent : Component
{
    public int Stacks;
    public TimeSpan LastKillTime;
}
