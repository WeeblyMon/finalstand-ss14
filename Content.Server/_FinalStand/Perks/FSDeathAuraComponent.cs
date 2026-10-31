namespace Content.Server._FinalStand.Perks;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSDeathAuraSystem))]
public sealed partial class FSDeathAuraComponent : Component
{
    public int Stacks;
    [AutoPausedField] public TimeSpan LastKillTime;
}
