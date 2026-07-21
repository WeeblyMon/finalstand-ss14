namespace Content.Server._FinalStand.Augments;

[RegisterComponent]
public sealed partial class FSDeathAuraComponent : Component
{
    public int Stacks;
    public TimeSpan LastKillTime;
}
