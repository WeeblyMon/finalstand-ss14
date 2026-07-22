namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSDeathAuraComponent : Component
{
    public int Stacks;
    public TimeSpan LastKillTime;
}
