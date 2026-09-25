namespace Content.Server._FinalStand.Perks;

/// <summary>Bloodload reload buff, on the mind.</summary>
[RegisterComponent]
public sealed partial class FSBloodloadComponent : Component
{
    public TimeSpan EndTime;
    public int Level;
}
