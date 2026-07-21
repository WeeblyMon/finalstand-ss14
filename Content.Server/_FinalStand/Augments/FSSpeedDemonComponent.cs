namespace Content.Server._FinalStand.Augments;

[RegisterComponent]
public sealed partial class FSSpeedDemonComponent : Component
{
    public int Stacks;
    public TimeSpan LastKillTime;
    public float DecayAccumulator;
}
