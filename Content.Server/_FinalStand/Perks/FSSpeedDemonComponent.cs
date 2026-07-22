namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSSpeedDemonComponent : Component
{
    public int Stacks;
    public TimeSpan LastKillTime;
    public float DecayAccumulator;
}
