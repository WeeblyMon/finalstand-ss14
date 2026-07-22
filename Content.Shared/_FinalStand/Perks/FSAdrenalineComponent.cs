namespace Content.Shared._FinalStand.Perks;

[RegisterComponent]
public sealed partial class FSAdrenalineComponent : Component
{
    public TimeSpan EndTime;
    public int LastSentSeconds = -1;
}
