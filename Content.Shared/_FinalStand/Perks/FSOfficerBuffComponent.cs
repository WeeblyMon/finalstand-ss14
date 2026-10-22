namespace Content.Shared._FinalStand.Perks;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FSOfficerBuffComponent : Component
{
    [AutoPausedField] public TimeSpan EndTime;
    public int Level;
}
