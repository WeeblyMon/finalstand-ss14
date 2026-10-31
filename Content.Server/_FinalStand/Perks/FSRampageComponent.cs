namespace Content.Server._FinalStand.Perks;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSRampageSystem))]
public sealed partial class FSRampageComponent : Component
{
    public int Stacks;
    [AutoPausedField] public TimeSpan LastKillTime;
}
