namespace Content.Server._FinalStand.Perks;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSAggravatorSystem))]
public sealed partial class FSAggravatorComponent : Component
{
    public int Stacks;
    [AutoPausedField] public TimeSpan NextDecay;
}
