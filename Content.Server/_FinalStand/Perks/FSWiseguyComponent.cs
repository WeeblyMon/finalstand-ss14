namespace Content.Server._FinalStand.Perks;

[RegisterComponent]
[Access(typeof(FSWiseguySystem))]
public sealed partial class FSWiseguyComponent : Component
{
    public int Streak;
    public bool DownedThisWave;
    public float Remainder;
}
