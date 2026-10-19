namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSSpeedDemonSystem))]
public sealed partial class FSSpeedDemonComponent : Component
{
    public int Stacks;
    public TimeSpan LastKillTime;
    public float DecayAccumulator;
}
