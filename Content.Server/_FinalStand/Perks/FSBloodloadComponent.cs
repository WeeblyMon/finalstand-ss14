namespace Content.Server._FinalStand.Perks;

/// <summary>Bloodload reload buff, on the mind.</summary>
[RegisterComponent, AutoGenerateComponentPause]
[Access(typeof(FSBloodloadSystem))]
public sealed partial class FSBloodloadComponent : Component
{
    [AutoPausedField] public TimeSpan EndTime;
    public int Level;
}
