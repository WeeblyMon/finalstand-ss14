namespace Content.Shared._FinalStand.Perks;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class FSAdrenalineComponent : Component
{
    [AutoPausedField] public TimeSpan EndTime;
    public int LastSentSeconds = -1;
}
