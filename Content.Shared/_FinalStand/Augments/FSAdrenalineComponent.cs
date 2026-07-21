namespace Content.Shared._FinalStand.Augments;

[RegisterComponent]
public sealed partial class FSAdrenalineComponent : Component
{
    public TimeSpan EndTime;
    public int LastSentSeconds = -1;
}
