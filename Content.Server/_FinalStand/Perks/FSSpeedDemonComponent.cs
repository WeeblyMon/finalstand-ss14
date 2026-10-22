namespace Content.Server._FinalStand.Perks;

[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSSpeedDemonSystem))]
public sealed partial class FSSpeedDemonComponent : Component
{
    public int Stacks;
    [AutoPausedField] public TimeSpan LastKillTime;
    public float DecayAccumulator;
}
